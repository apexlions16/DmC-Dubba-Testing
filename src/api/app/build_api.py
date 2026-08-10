from __future__ import annotations

import hashlib
import os
import tempfile
from datetime import UTC, datetime
from typing import Annotated

from fastapi import APIRouter, Depends, File, HTTPException, UploadFile, status
from fastapi.responses import StreamingResponse
from sqlalchemy import select
from sqlalchemy.orm import Session

from . import models
from .db import get_db
from .main import AdminUser, CurrentUser, DeveloperUser
from .storage import HfBucketStorage

router = APIRouter(tags=["test-surumu-dosyalari"])
storage = HfBucketStorage()
DB = Annotated[Session, Depends(get_db)]
Upload = Annotated[UploadFile, File()]


@router.post(
    "/projects/{project_id}/builds/{build_id}/file",
    status_code=status.HTTP_201_CREATED,
)
async def upload_build_file(
    project_id: str,
    build_id: str,
    db: DB,
    developer: DeveloperUser,
    file: Upload,
) -> dict:
    build = db.get(models.Build, build_id)
    if build is None or build.project_id != project_id:
        raise HTTPException(status_code=404, detail="Test sürümü bulunamadı.")
    if build.status == models.BuildStatus.ARCHIVED:
        raise HTTPException(status_code=409, detail="Arşivlenmiş test sürümünün dosyası değiştirilemez.")
    if build.storage_path:
        raise HTTPException(status_code=409, detail="Bu test sürümü için daha önce dosya yüklenmiş.")
    if not file.filename:
        raise HTTPException(status_code=400, detail="Dosya adı zorunludur.")

    temp_path: str | None = None
    try:
        sha = hashlib.sha256()
        size_bytes = 0
        with tempfile.NamedTemporaryFile(prefix="qa-build-", suffix=".upload", delete=False) as temp:
            temp_path = temp.name
            while chunk := await file.read(4 * 1024 * 1024):
                temp.write(chunk)
                sha.update(chunk)
                size_bytes += len(chunk)

        remote_path = storage.active_build_path(project_id, build_id, file.filename)
        storage.upload_file(temp_path, remote_path)

        build.storage_path = remote_path
        build.original_filename = file.filename
        build.sha256 = sha.hexdigest()
        build.size_bytes = size_bytes
        build.status = models.BuildStatus.CANDIDATE
        db.add(
            models.AuditEvent(
                project_id=project_id,
                actor_user_id=developer.id,
                action="build_file_uploaded",
                entity_type="build",
                entity_id=build.id,
                payload={
                    "filename": file.filename,
                    "size_bytes": size_bytes,
                    "sha256": build.sha256,
                    "storage_path": remote_path,
                },
            )
        )
        db.commit()
        return {
            "build_id": build.id,
            "storage_path": remote_path,
            "size_bytes": size_bytes,
            "sha256": build.sha256,
            "status": build.status.value,
        }
    finally:
        await file.close()
        if temp_path and os.path.exists(temp_path):
            os.remove(temp_path)


@router.get("/projects/{project_id}/builds/{build_id}/download")
def download_build(
    project_id: str,
    build_id: str,
    db: DB,
    user: CurrentUser,
) -> StreamingResponse:
    build = db.get(models.Build, build_id)
    if build is None or build.project_id != project_id or not build.storage_path:
        raise HTTPException(status_code=404, detail="Test sürümü dosyası bulunamadı.")

    filename = build.original_filename or f"build-{build.version}.zip"
    headers = {"Content-Disposition": f'attachment; filename="{filename}"'}
    if build.size_bytes is not None:
        headers["Content-Length"] = str(build.size_bytes)

    db.add(
        models.AuditEvent(
            project_id=project_id,
            actor_user_id=user.id,
            action="build_download_started",
            entity_type="build",
            entity_id=build.id,
            payload={"version": build.version},
        )
    )
    db.commit()

    return StreamingResponse(
        storage.iter_chunks(build.storage_path),
        media_type="application/octet-stream",
        headers=headers,
    )


@router.post(
    "/projects/{project_id}/builds/{build_id}/events/{event_type}",
    status_code=status.HTTP_204_NO_CONTENT,
)
def record_build_user_event(
    project_id: str,
    build_id: str,
    event_type: str,
    db: DB,
    user: CurrentUser,
) -> None:
    allowed = {"download_completed", "installed", "installation_failed"}
    if event_type not in allowed:
        raise HTTPException(status_code=400, detail="Desteklenmeyen test sürümü olayı.")

    build = db.get(models.Build, build_id)
    if build is None or build.project_id != project_id:
        raise HTTPException(status_code=404, detail="Test sürümü bulunamadı.")

    db.add(
        models.AuditEvent(
            project_id=project_id,
            actor_user_id=user.id,
            action=f"build_{event_type}",
            entity_type="build",
            entity_id=build.id,
            payload={"version": build.version},
        )
    )
    db.commit()


@router.post(
    "/projects/{project_id}/builds/{build_id}/archive",
    status_code=status.HTTP_204_NO_CONTENT,
)
def archive_build(
    project_id: str,
    build_id: str,
    db: DB,
    admin: AdminUser,
) -> None:
    build = db.get(models.Build, build_id)
    if build is None or build.project_id != project_id:
        raise HTTPException(status_code=404, detail="Test sürümü bulunamadı.")
    if build.status == models.BuildStatus.CURRENT:
        raise HTTPException(status_code=409, detail="Güncel Test Sürümü arşivlenemez. Önce başka bir sürümü güncel yapın.")
    if build.status == models.BuildStatus.ARCHIVED:
        return
    if not build.storage_path or not build.original_filename:
        raise HTTPException(status_code=409, detail="Bu test sürümüne ait saklanmış dosya yok.")

    open_task = db.scalar(
        select(models.Task.id)
        .where(
            models.Task.required_build_id == build.id,
            models.Task.status == models.TaskStatus.OPEN,
        )
        .limit(1)
    )
    if open_task:
        raise HTTPException(status_code=409, detail="Bu test sürümü hâlâ açık bir görev tarafından kullanılıyor ve arşivlenemez.")

    archived_path = storage.archive_build(project_id, build.id, build.original_filename)
    old_path = build.storage_path
    build.storage_path = archived_path
    build.status = models.BuildStatus.ARCHIVED
    build.archived_at = datetime.now(UTC)
    db.add(
        models.AuditEvent(
            project_id=project_id,
            actor_user_id=admin.id,
            action="build_archived",
            entity_type="build",
            entity_id=build.id,
            payload={"from": old_path, "to": archived_path, "version": build.version},
        )
    )
    db.commit()
