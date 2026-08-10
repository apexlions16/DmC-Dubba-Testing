from __future__ import annotations

import hashlib
import os
import tempfile
from typing import Annotated

from fastapi import APIRouter, File, Form, HTTPException, Query, UploadFile, status
from fastapi.responses import StreamingResponse
from sqlalchemy import select

from . import models
from .auth import AdminUser, DB
from .storage import HfBucketStorage, safe_segment

router = APIRouter(prefix="/client/releases", tags=["istemci-surumleri"])
storage = HfBucketStorage()


@router.get("/latest")
def latest_release(
    db: DB,
    channel: str = Query(default="stable", pattern="^(stable|beta)$"),
) -> dict:
    release = db.scalar(
        select(models.ClientRelease)
        .where(models.ClientRelease.channel == channel)
        .order_by(models.ClientRelease.published_at.desc())
        .limit(1)
    )
    if release is None:
        raise HTTPException(status_code=404, detail="Bu kanal için henüz istemci sürümü yayınlanmadı.")

    return {
        "channel": release.channel,
        "version": release.version,
        "title": release.title,
        "notes": release.notes,
        "artifact_url": release.artifact_url,
        "sha256": release.sha256,
        "minimum_version": release.minimum_version,
        "mandatory": release.mandatory,
        "published_at": release.published_at,
    }


@router.get("")
def release_history(
    db: DB,
    channel: str = Query(default="stable", pattern="^(stable|beta)$"),
) -> list[dict]:
    releases = db.scalars(
        select(models.ClientRelease)
        .where(models.ClientRelease.channel == channel)
        .order_by(models.ClientRelease.published_at.desc())
    ).all()
    return [
        {
            "id": release.id,
            "channel": release.channel,
            "version": release.version,
            "title": release.title,
            "notes": release.notes,
            "artifact_url": release.artifact_url,
            "sha256": release.sha256,
            "minimum_version": release.minimum_version,
            "mandatory": release.mandatory,
            "published_at": release.published_at,
        }
        for release in releases
    ]


@router.post("/publish", status_code=status.HTTP_201_CREATED)
async def publish_client_release(
    db: DB,
    admin: AdminUser,
    file: Annotated[UploadFile, File()],
    channel: Annotated[str, Form(pattern="^(stable|beta)$")] = "stable",
    version: Annotated[str, Form(min_length=1, max_length=80)] = "",
    title: Annotated[str, Form(min_length=1, max_length=200)] = "",
    notes: Annotated[str, Form(max_length=20000)] = "",
    minimum_version: Annotated[str | None, Form(max_length=80)] = None,
    mandatory: Annotated[bool, Form()] = False,
) -> dict:
    clean_version = version.strip()
    clean_title = title.strip()
    if not clean_version or not clean_title:
        raise HTTPException(status_code=400, detail="Sürüm ve başlık zorunludur.")
    if not file.filename:
        raise HTTPException(status_code=400, detail="İstemci sürümü ZIP dosyası zorunludur.")
    if not file.filename.lower().endswith(".zip"):
        raise HTTPException(status_code=400, detail="İstemci güncelleme paketi ZIP biçiminde olmalıdır.")
    duplicate = db.scalar(select(models.ClientRelease.id).where(
        models.ClientRelease.channel == channel,
        models.ClientRelease.version == clean_version,
    ))
    if duplicate:
        raise HTTPException(status_code=409, detail="Bu kanal ve sürüm numarası daha önce yayınlanmış.")

    release_id = models.new_id()
    remote_path = "/".join([
        "client-releases",
        safe_segment(channel),
        safe_segment(clean_version),
        f"{safe_segment(release_id)}-{safe_segment(file.filename)}",
    ])
    temp_path: str | None = None
    try:
        sha = hashlib.sha256()
        size_bytes = 0
        with tempfile.NamedTemporaryFile(prefix="qa-client-release-", suffix=".zip", delete=False) as temp:
            temp_path = temp.name
            while chunk := await file.read(4 * 1024 * 1024):
                temp.write(chunk)
                sha.update(chunk)
                size_bytes += len(chunk)
        storage.upload_file(temp_path, remote_path)

        release = models.ClientRelease(
            id=release_id,
            channel=channel,
            version=clean_version,
            title=clean_title,
            notes=notes.strip(),
            artifact_url=f"/client/releases/{release_id}/download",
            sha256=sha.hexdigest(),
            minimum_version=minimum_version.strip() if minimum_version else None,
            mandatory=mandatory,
            published_by=admin.id,
        )
        db.add(release)
        db.add(models.AuditEvent(
            project_id=None,
            actor_user_id=admin.id,
            action="client_release_published",
            entity_type="client_release",
            entity_id=release.id,
            payload={
                "channel": channel,
                "version": clean_version,
                "filename": file.filename,
                "size_bytes": size_bytes,
                "sha256": release.sha256,
                "storage_path": remote_path,
            },
        ))
        db.commit()
        return {
            "id": release.id,
            "channel": release.channel,
            "version": release.version,
            "title": release.title,
            "sha256": release.sha256,
            "size_bytes": size_bytes,
            "artifact_url": release.artifact_url,
        }
    except Exception:
        try:
            storage.delete_file(remote_path)
        except Exception:
            pass
        raise
    finally:
        await file.close()
        if temp_path and os.path.exists(temp_path):
            os.remove(temp_path)


@router.get("/{release_id}/download")
def download_client_release(release_id: str, db: DB) -> StreamingResponse:
    release = db.get(models.ClientRelease, release_id)
    if release is None:
        raise HTTPException(status_code=404, detail="İstemci sürümü bulunamadı.")
    audit = db.scalar(select(models.AuditEvent).where(
        models.AuditEvent.entity_type == "client_release",
        models.AuditEvent.entity_id == release.id,
        models.AuditEvent.action == "client_release_published",
    ).order_by(models.AuditEvent.created_at.desc()).limit(1))
    payload = audit.payload if audit and audit.payload else {}
    storage_path = payload.get("storage_path")
    filename = payload.get("filename") or f"GameQA-{release.version}.zip"
    if not storage_path:
        raise HTTPException(status_code=404, detail="İstemci sürümü dosyası bulunamadı.")
    return StreamingResponse(
        storage.iter_chunks(storage_path),
        media_type="application/zip",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )
