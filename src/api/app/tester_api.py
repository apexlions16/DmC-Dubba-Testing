from __future__ import annotations

from fastapi import APIRouter, HTTPException
from sqlalchemy import select

from . import models
from .auth import CurrentUser, DB

router = APIRouter(tags=["tester-paneli"])


def _ensure_member(db: DB, project_id: str, user: models.User) -> None:
    if user.role in {models.UserRole.ADMIN, models.UserRole.SUPER_ADMIN}:
        return
    membership = db.scalar(select(models.ProjectMember.id).where(
        models.ProjectMember.project_id == project_id,
        models.ProjectMember.user_id == user.id,
    ))
    if membership is None:
        raise HTTPException(status_code=403, detail="Bu projeye erişim yetkiniz yok.")


@router.get("/projects/{project_id}/builds/current-detail")
def current_build_detail(project_id: str, db: DB, user: CurrentUser) -> dict:
    _ensure_member(db, project_id, user)
    build = db.scalar(select(models.Build).where(
        models.Build.project_id == project_id,
        models.Build.status == models.BuildStatus.CURRENT,
    ).order_by(models.Build.published_at.desc()).limit(1))
    if build is None:
        raise HTTPException(status_code=404, detail="Bu proje için yayınlanmış güncel test sürümü yok.")
    return {
        "id": build.id,
        "version": build.version,
        "title": build.title,
        "description": build.description,
        "installation_instructions": build.installation_instructions,
        "changelog": build.changelog,
        "original_filename": build.original_filename,
        "size_bytes": build.size_bytes,
        "sha256": build.sha256,
        "published_at": build.published_at,
    }


@router.get("/tasks/{task_id}/shared")
def shared_task_detail(task_id: str, db: DB, user: CurrentUser) -> dict:
    task = db.get(models.Task, task_id)
    if task is None:
        raise HTTPException(status_code=404, detail="Görev bulunamadı.")
    _ensure_member(db, task.project_id, user)
    assigned = db.scalar(select(models.TaskAssignee.id).where(
        models.TaskAssignee.task_id == task.id,
        models.TaskAssignee.user_id == user.id,
    ))
    if assigned is None and user.role == models.UserRole.TESTER:
        raise HTTPException(status_code=403, detail="Bu görev size atanmadı.")

    assignee_ids = list(db.scalars(select(models.TaskAssignee.user_id).where(
        models.TaskAssignee.task_id == task.id
    )).all())
    bug_count = len(list(db.scalars(select(models.BugReport.id).where(
        models.BugReport.task_id == task.id
    )).all()))
    return {
        "id": task.id,
        "title": task.title,
        "description": task.description,
        "deadline_at": task.deadline_at,
        "status": task.status.value,
        "assignees": [
            member.display_name for user_id in assignee_ids
            if (member := db.get(models.User, user_id)) is not None
        ],
        "known_report_count": bug_count,
    }
