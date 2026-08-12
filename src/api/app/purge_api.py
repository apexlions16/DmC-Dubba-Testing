from __future__ import annotations

import hashlib
import secrets
from datetime import UTC, datetime

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, Field
from sqlalchemy import delete, func, select

from . import models
from .auth import AdminUser, DB
from .notification_models import InboxNotification, InboxRecipient
from .storage import HfBucketStorage

router = APIRouter(tags=["guvenli-silme"])
storage = HfBucketStorage()


class PurgeCreateRequest(BaseModel):
    project_name: str = Field(min_length=1, max_length=240)
    delete_storage: bool = True
    delete_database: bool = True
    delete_audit_tombstone: bool = False


class PurgeApproveRequest(BaseModel):
    project_name: str = Field(min_length=1, max_length=240)
    confirmation_code: str = Field(min_length=8, max_length=80)


def _project_or_404(db: DB, project_id: str) -> models.Project:
    project = db.get(models.Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Proje bulunamadı.")
    return project


def _preview(db: DB, project_id: str) -> dict:
    bug_ids = list(db.scalars(select(models.BugReport.id).where(models.BugReport.project_id == project_id)).all())
    retest_count = 0
    if bug_ids:
        retest_count = db.scalar(select(func.count()).select_from(models.RetestRequest).where(
            models.RetestRequest.bug_report_id.in_(bug_ids)
        )) or 0
    evidence = db.scalars(select(models.EvidenceAsset).where(models.EvidenceAsset.project_id == project_id)).all()
    builds = db.scalars(select(models.Build).where(models.Build.project_id == project_id)).all()
    return {
        "bug_reports": len(bug_ids),
        "evidence_files": len(evidence),
        "retests": retest_count,
        "tasks": db.scalar(select(func.count()).select_from(models.Task).where(models.Task.project_id == project_id)) or 0,
        "builds": len(builds),
        "project_members": db.scalar(select(func.count()).select_from(models.ProjectMember).where(models.ProjectMember.project_id == project_id)) or 0,
        "sections": db.scalar(select(func.count()).select_from(models.Section).where(models.Section.project_id == project_id)) or 0,
        "estimated_storage_bytes": sum(item.size_bytes or 0 for item in evidence) + sum(item.size_bytes or 0 for item in builds),
    }


@router.get("/admin/projects/{project_id}/purge-preview")
def purge_preview(project_id: str, db: DB, admin: AdminUser) -> dict:
    project = _project_or_404(db, project_id)
    return {"project_id": project.id, "project_name": project.name, "status": project.status.value, **_preview(db, project_id)}


@router.post("/admin/projects/{project_id}/purge-requests", status_code=status.HTTP_201_CREATED)
def create_purge_request(project_id: str, payload: PurgeCreateRequest, db: DB, admin: AdminUser) -> dict:
    project = _project_or_404(db, project_id)
    if project.status != models.ProjectStatus.CLOSED:
        raise HTTPException(status_code=409, detail="Kalıcı silme talebi oluşturmadan önce proje kapatılmalıdır.")
    if payload.project_name.strip() != project.name:
        raise HTTPException(status_code=400, detail="Yazılan proje adı proje adıyla birebir eşleşmiyor.")
    existing = db.scalar(select(models.PurgeRequest).where(
        models.PurgeRequest.project_id == project_id,
        models.PurgeRequest.status.in_([
            models.PurgeStatus.REQUESTED,
            models.PurgeStatus.SECOND_ADMIN_APPROVED,
            models.PurgeStatus.RUNNING,
        ]),
    ))
    if existing is not None:
        raise HTTPException(status_code=409, detail="Bu proje için zaten aktif bir kalıcı silme talebi var.")

    code = f"DELETE-{secrets.token_hex(4).upper()}"
    request = models.PurgeRequest(
        project_id=project_id,
        requested_by=admin.id,
        confirmation_code_hash=hashlib.sha256(code.encode("utf-8")).hexdigest(),
        delete_storage=payload.delete_storage,
        delete_database=payload.delete_database,
        delete_audit_tombstone=payload.delete_audit_tombstone,
        preview_json=_preview(db, project_id),
    )
    project.status = models.ProjectStatus.PURGE_PENDING
    db.add(request)
    db.flush()
    db.add(models.AuditEvent(
        project_id=project_id,
        actor_user_id=admin.id,
        action="purge_requested",
        entity_type="purge_request",
        entity_id=request.id,
        payload={"preview": request.preview_json},
    ))
    db.commit()
    return {
        "id": request.id,
        "project_id": project.id,
        "project_name": project.name,
        "confirmation_code": code,
        "preview": request.preview_json,
        "warning": "Bu kod yalnız ikinci farklı yönetici tarafından onay sırasında kullanılmalıdır.",
    }


@router.get("/admin/purge-requests")
def list_purge_requests(db: DB, admin: AdminUser) -> list[dict]:
    requests = db.scalars(select(models.PurgeRequest).where(
        models.PurgeRequest.status.in_([
            models.PurgeStatus.REQUESTED,
            models.PurgeStatus.SECOND_ADMIN_APPROVED,
            models.PurgeStatus.RUNNING,
            models.PurgeStatus.FAILED,
        ])
    ).order_by(models.PurgeRequest.requested_at.desc())).all()
    result: list[dict] = []
    for item in requests:
        project = db.get(models.Project, item.project_id)
        requester = db.get(models.User, item.requested_by)
        result.append({
            "id": item.id,
            "project_id": item.project_id,
            "project_name": project.name if project else "Silinmiş proje",
            "requested_by": requester.display_name if requester else "Bilinmiyor",
            "status": item.status.value,
            "preview": item.preview_json,
            "requested_at": item.requested_at,
        })
    return result


def _execute_database_purge(db: DB, request: models.PurgeRequest, project: models.Project, second_admin: models.User) -> None:
    project_id = project.id
    project_name = project.name
    preview = dict(request.preview_json)
    requested_by = request.requested_by
    bug_ids = list(db.scalars(select(models.BugReport.id).where(models.BugReport.project_id == project_id)).all())
    task_ids = list(db.scalars(select(models.Task.id).where(models.Task.project_id == project_id)).all())
    build_ids = list(db.scalars(select(models.Build.id).where(models.Build.project_id == project_id)).all())
    retest_ids = list(db.scalars(select(models.RetestRequest.id).where(
        models.RetestRequest.bug_report_id.in_(bug_ids) if bug_ids else False
    )).all()) if bug_ids else []
    old_notification_ids = list(db.scalars(select(models.Notification.id).where(models.Notification.project_id == project_id)).all())
    inbox_ids = list(db.scalars(select(InboxNotification.id).where(InboxNotification.project_id == project_id)).all())

    if retest_ids:
        db.execute(delete(models.RetestResult).where(models.RetestResult.retest_request_id.in_(retest_ids)))
        db.execute(delete(models.RetestAssignee).where(models.RetestAssignee.retest_request_id.in_(retest_ids)))
        db.execute(delete(models.RetestRequest).where(models.RetestRequest.id.in_(retest_ids)))
    db.execute(delete(models.EvidenceAsset).where(models.EvidenceAsset.project_id == project_id))
    if bug_ids:
        db.execute(delete(models.BugEvent).where(models.BugEvent.bug_report_id.in_(bug_ids)))
        db.execute(delete(models.BuildFixCandidate).where(models.BuildFixCandidate.bug_report_id.in_(bug_ids)))
        db.execute(delete(models.BugReport).where(models.BugReport.id.in_(bug_ids)))
    if task_ids:
        db.execute(delete(models.TaskAssignee).where(models.TaskAssignee.task_id.in_(task_ids)))
        db.execute(delete(models.Task).where(models.Task.id.in_(task_ids)))
    if old_notification_ids:
        db.execute(delete(models.NotificationReceipt).where(models.NotificationReceipt.notification_id.in_(old_notification_ids)))
        db.execute(delete(models.Notification).where(models.Notification.id.in_(old_notification_ids)))
    if inbox_ids:
        db.execute(delete(InboxRecipient).where(InboxRecipient.notification_id.in_(inbox_ids)))
        db.execute(delete(InboxNotification).where(InboxNotification.id.in_(inbox_ids)))
    db.execute(delete(models.ProjectMember).where(models.ProjectMember.project_id == project_id))
    db.execute(delete(models.Section).where(models.Section.project_id == project_id))
    if build_ids:
        db.execute(delete(models.BuildFixCandidate).where(models.BuildFixCandidate.build_id.in_(build_ids)))
        db.execute(delete(models.Build).where(models.Build.id.in_(build_ids)))

    db.execute(delete(models.AuditEvent).where(models.AuditEvent.project_id == project_id))
    db.execute(delete(models.PurgeRequest).where(models.PurgeRequest.project_id == project_id))
    db.delete(project)
    if not request.delete_audit_tombstone:
        db.add(models.AuditEvent(
            project_id=None,
            actor_user_id=second_admin.id,
            action="project_purged_tombstone",
            entity_type="project",
            entity_id=project_id,
            payload={
                "project_name": project_name,
                "requested_by": requested_by,
                "approved_by": second_admin.id,
                "preview": preview,
                "purged_at": datetime.now(UTC).isoformat(),
            },
        ))
    db.commit()


@router.post("/admin/purge-requests/{request_id}/approve")
def approve_purge_request(request_id: str, payload: PurgeApproveRequest, db: DB, admin: AdminUser) -> dict:
    request = db.get(models.PurgeRequest, request_id)
    if request is None:
        raise HTTPException(status_code=404, detail="Kalıcı silme talebi bulunamadı.")
    if request.status != models.PurgeStatus.REQUESTED:
        raise HTTPException(status_code=409, detail="Bu kalıcı silme talebi artık onay beklemiyor.")
    if request.requested_by == admin.id:
        raise HTTPException(status_code=409, detail="Kalıcı silme talebini oluşturan yönetici ikinci onayı veremez.")
    project = _project_or_404(db, request.project_id)
    if payload.project_name.strip() != project.name:
        raise HTTPException(status_code=400, detail="Yazılan proje adı proje adıyla birebir eşleşmiyor.")
    submitted_hash = hashlib.sha256(payload.confirmation_code.strip().upper().encode("utf-8")).hexdigest()
    if not secrets.compare_digest(submitted_hash, request.confirmation_code_hash):
        raise HTTPException(status_code=400, detail="Kalıcı silme doğrulama kodu geçersiz.")

    request.second_admin_id = admin.id
    request.approved_at = datetime.now(UTC)
    request.status = models.PurgeStatus.RUNNING
    db.commit()

    deleted_files = 0
    try:
        if request.delete_storage:
            deleted_files = storage.delete_prefix(f"projects/{project.id}")
        if request.delete_database:
            _execute_database_purge(db, request, project, admin)
        else:
            request.status = models.PurgeStatus.COMPLETED
            request.completed_at = datetime.now(UTC)
            project.status = models.ProjectStatus.PURGED
            db.commit()
        return {"status": "completed", "deleted_storage_files": deleted_files, "preview": request.preview_json}
    except Exception as exc:
        db.rollback()
        live_request = db.get(models.PurgeRequest, request_id)
        live_project = db.get(models.Project, request.project_id)
        if live_request is not None:
            live_request.status = models.PurgeStatus.FAILED
        if live_project is not None:
            live_project.status = models.ProjectStatus.CLOSED
        db.commit()
        raise HTTPException(status_code=500, detail=f"Kalıcı silme tamamlanamadı: {exc}") from exc


@router.post("/admin/purge-requests/{request_id}/cancel", status_code=status.HTTP_204_NO_CONTENT)
def cancel_purge_request(request_id: str, db: DB, admin: AdminUser) -> None:
    request = db.get(models.PurgeRequest, request_id)
    if request is None:
        raise HTTPException(status_code=404, detail="Kalıcı silme talebi bulunamadı.")
    if request.status != models.PurgeStatus.REQUESTED:
        raise HTTPException(status_code=409, detail="Bu talep artık iptal edilemez.")
    project = db.get(models.Project, request.project_id)
    request.status = models.PurgeStatus.CANCELLED
    if project is not None:
        project.status = models.ProjectStatus.CLOSED
    db.commit()
