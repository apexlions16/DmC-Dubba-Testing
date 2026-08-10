from __future__ import annotations

import hashlib
import os
import tempfile

from fastapi import APIRouter, File, HTTPException, UploadFile, status
from fastapi.responses import StreamingResponse
from sqlalchemy import select

from . import models
from .auth import CurrentUser, DB
from .db import settings
from .storage import HfBucketStorage

router = APIRouter(tags=["video-kanitlari"])
storage = HfBucketStorage()


async def _store_upload(file: UploadFile, remote_path: str) -> tuple[int, str]:
    if not file.filename:
        raise HTTPException(status_code=400, detail="Dosya adı bulunamadı.")

    temp_path: str | None = None
    try:
        sha = hashlib.sha256()
        size_bytes = 0
        with tempfile.NamedTemporaryFile(prefix="qa-evidence-", suffix=".upload", delete=False) as temp:
            temp_path = temp.name
            while chunk := await file.read(4 * 1024 * 1024):
                size_bytes += len(chunk)
                if size_bytes > settings.max_evidence_upload_bytes:
                    raise HTTPException(
                        status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                        detail=(
                            "Video dosyası izin verilen en yüksek boyutu aşıyor. "
                            f"Sınır: {settings.max_evidence_upload_bytes // (1024 ** 3)} GB."
                        ),
                    )
                sha.update(chunk)
                temp.write(chunk)

        if size_bytes == 0:
            raise HTTPException(status_code=400, detail="Boş dosya yüklenemez.")

        storage.upload_file(temp_path, remote_path)
        return size_bytes, sha.hexdigest()
    finally:
        await file.close()
        if temp_path and os.path.exists(temp_path):
            os.remove(temp_path)


def _ensure_project_access(db: DB, project_id: str, user: models.User) -> None:
    if user.role in {models.UserRole.ADMIN, models.UserRole.SUPER_ADMIN}:
        return

    membership = db.scalar(
        select(models.ProjectMember.id).where(
            models.ProjectMember.project_id == project_id,
            models.ProjectMember.user_id == user.id,
        )
    )
    if membership is None:
        raise HTTPException(status_code=403, detail="Bu projenin dosyalarına erişim yetkiniz yok.")


@router.post(
    "/bugs/{bug_id}/evidence",
    status_code=status.HTTP_201_CREATED,
)
async def upload_bug_evidence(
    bug_id: str,
    db: DB,
    user: CurrentUser,
    file: UploadFile = File(...),
) -> dict:
    bug = db.get(models.BugReport, bug_id)
    if bug is None:
        raise HTTPException(status_code=404, detail="Hata raporu bulunamadı.")

    _ensure_project_access(db, bug.project_id, user)
    if bug.reporter_id != user.id and user.role not in {
        models.UserRole.DEVELOPER,
        models.UserRole.ADMIN,
        models.UserRole.SUPER_ADMIN,
    }:
        raise HTTPException(
            status_code=403,
            detail="Başka bir test ekibi üyesinin hata raporuna dosya yükleyemezsiniz.",
        )

    if not file.filename:
        raise HTTPException(status_code=400, detail="Dosya adı bulunamadı.")

    asset_id = models.new_id()
    remote_path = storage.bug_evidence_path(
        bug.project_id,
        bug.public_key,
        asset_id,
        file.filename,
    )
    size_bytes, sha256 = await _store_upload(file, remote_path)

    asset = models.EvidenceAsset(
        id=asset_id,
        project_id=bug.project_id,
        owner_type="bug_report",
        owner_id=bug.id,
        storage_path=remote_path,
        original_filename=file.filename,
        media_type=file.content_type or "application/octet-stream",
        size_bytes=size_bytes,
        sha256=sha256,
        uploaded_by=user.id,
    )
    db.add(asset)
    db.add(
        models.AuditEvent(
            project_id=bug.project_id,
            actor_user_id=user.id,
            action="bug_evidence_uploaded",
            entity_type="evidence_asset",
            entity_id=asset.id,
            payload={
                "bug_id": bug.id,
                "bug_key": bug.public_key,
                "filename": asset.original_filename,
                "size_bytes": size_bytes,
                "sha256": sha256,
            },
        )
    )
    db.commit()

    return {
        "id": asset.id,
        "bug_id": bug.id,
        "bug_key": bug.public_key,
        "filename": asset.original_filename,
        "media_type": asset.media_type,
        "size_bytes": asset.size_bytes,
        "sha256": asset.sha256,
        "storage_path": asset.storage_path,
    }


@router.post(
    "/retests/{retest_request_id}/evidence",
    status_code=status.HTTP_201_CREATED,
)
async def upload_retest_evidence(
    retest_request_id: str,
    db: DB,
    user: CurrentUser,
    file: UploadFile = File(...),
) -> dict:
    request = db.get(models.RetestRequest, retest_request_id)
    if request is None:
        raise HTTPException(status_code=404, detail="Yeniden test talebi bulunamadı.")

    bug = db.get(models.BugReport, request.bug_report_id)
    if bug is None:
        raise HTTPException(status_code=404, detail="Yeniden teste bağlı hata raporu bulunamadı.")

    _ensure_project_access(db, bug.project_id, user)
    if user.role not in {
        models.UserRole.ADMIN,
        models.UserRole.SUPER_ADMIN,
    }:
        assigned = db.scalar(
            select(models.RetestAssignee.id).where(
                models.RetestAssignee.retest_request_id == request.id,
                models.RetestAssignee.user_id == user.id,
            )
        )
        if assigned is None:
            raise HTTPException(status_code=403, detail="Bu yeniden test size atanmadı.")

    if not file.filename:
        raise HTTPException(status_code=400, detail="Dosya adı bulunamadı.")

    asset_id = models.new_id()
    remote_path = storage.retest_evidence_path(
        bug.project_id,
        request.id,
        asset_id,
        file.filename,
    )
    size_bytes, sha256 = await _store_upload(file, remote_path)

    asset = models.EvidenceAsset(
        id=asset_id,
        project_id=bug.project_id,
        owner_type="retest_request",
        owner_id=request.id,
        storage_path=remote_path,
        original_filename=file.filename,
        media_type=file.content_type or "application/octet-stream",
        size_bytes=size_bytes,
        sha256=sha256,
        uploaded_by=user.id,
    )
    db.add(asset)
    db.add(
        models.AuditEvent(
            project_id=bug.project_id,
            actor_user_id=user.id,
            action="retest_evidence_uploaded",
            entity_type="evidence_asset",
            entity_id=asset.id,
            payload={
                "retest_request_id": request.id,
                "bug_id": bug.id,
                "filename": asset.original_filename,
                "size_bytes": size_bytes,
                "sha256": sha256,
            },
        )
    )
    db.commit()

    return {
        "id": asset.id,
        "retest_request_id": request.id,
        "filename": asset.original_filename,
        "media_type": asset.media_type,
        "size_bytes": asset.size_bytes,
        "sha256": asset.sha256,
        "storage_path": asset.storage_path,
    }


@router.get("/bugs/{bug_id}/evidence")
def list_bug_evidence(bug_id: str, db: DB, user: CurrentUser) -> list[dict]:
    bug = db.get(models.BugReport, bug_id)
    if bug is None:
        raise HTTPException(status_code=404, detail="Hata raporu bulunamadı.")

    _ensure_project_access(db, bug.project_id, user)
    assets = db.scalars(
        select(models.EvidenceAsset)
        .where(
            models.EvidenceAsset.owner_type == "bug_report",
            models.EvidenceAsset.owner_id == bug.id,
        )
        .order_by(models.EvidenceAsset.created_at.asc())
    ).all()
    return [
        {
            "id": asset.id,
            "filename": asset.original_filename,
            "media_type": asset.media_type,
            "size_bytes": asset.size_bytes,
            "sha256": asset.sha256,
            "uploaded_by": asset.uploaded_by,
            "created_at": asset.created_at,
        }
        for asset in assets
    ]


@router.get("/evidence/{asset_id}/download")
def download_evidence(asset_id: str, db: DB, user: CurrentUser) -> StreamingResponse:
    asset = db.get(models.EvidenceAsset, asset_id)
    if asset is None:
        raise HTTPException(status_code=404, detail="Video veya kanıt dosyası bulunamadı.")

    _ensure_project_access(db, asset.project_id, user)
    headers = {"Content-Disposition": f'inline; filename="{asset.original_filename}"'}
    if asset.size_bytes is not None:
        headers["Content-Length"] = str(asset.size_bytes)

    return StreamingResponse(
        storage.iter_chunks(asset.storage_path),
        media_type=asset.media_type,
        headers=headers,
    )
