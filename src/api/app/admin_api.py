from __future__ import annotations

from collections import Counter
from typing import Literal

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import BaseModel, Field
from sqlalchemy import func, select

from . import models
from .auth import AdminUser, CurrentUser, DB, DeveloperUser
from .storage import HfBucketStorage

router = APIRouter(tags=["yonetim-merkezi"])
storage = HfBucketStorage()


class SectionCreateRequest(BaseModel):
    name: str = Field(min_length=1, max_length=150)
    sort_order: int = 0


class ProjectMembersRequest(BaseModel):
    user_ids: list[str] = Field(default_factory=list)


class TaskStatusRequest(BaseModel):
    status: Literal["open", "completed", "cancelled"]


def _project(db: DB, project_id: str) -> models.Project:
    project = db.get(models.Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Proje bulunamadı.")
    return project


@router.get("/bugs/mine")
def my_bug_reports(db: DB, user: CurrentUser, project_id: str | None = None) -> list[dict]:
    query = select(models.BugReport).where(models.BugReport.reporter_id == user.id)
    if project_id:
        query = query.where(models.BugReport.project_id == project_id)
    bugs = db.scalars(query.order_by(models.BugReport.created_at.desc()).limit(250)).all()
    return [{
        "id": bug.id,
        "key": bug.public_key,
        "project_id": bug.project_id,
        "title": bug.title,
        "bug_type": bug.bug_type,
        "status": bug.status.value,
        "root_cause": bug.root_cause,
        "created_at": bug.created_at,
    } for bug in bugs]


@router.get("/admin/projects/{project_id}/bugs")
def admin_bug_list(
    project_id: str,
    db: DB,
    developer: DeveloperUser,
    status_value: str | None = Query(default=None, alias="status"),
    bug_type: str | None = None,
    reporter_id: str | None = None,
    build_id: str | None = None,
    search: str | None = None,
) -> list[dict]:
    _project(db, project_id)
    query = select(models.BugReport).where(models.BugReport.project_id == project_id)
    if status_value:
        try:
            query = query.where(models.BugReport.status == models.BugStatus(status_value))
        except ValueError as exc:
            raise HTTPException(status_code=400, detail="Geçersiz hata durumu.") from exc
    if bug_type:
        query = query.where(models.BugReport.bug_type == bug_type)
    if reporter_id:
        query = query.where(models.BugReport.reporter_id == reporter_id)
    if build_id:
        query = query.where(models.BugReport.reported_build_id == build_id)
    if search:
        value = f"%{search.strip()}%"
        query = query.where(
            models.BugReport.title.ilike(value)
            | models.BugReport.public_key.ilike(value)
            | models.BugReport.description.ilike(value)
        )

    bugs = db.scalars(query.order_by(models.BugReport.created_at.desc()).limit(1000)).all()
    result: list[dict] = []
    for bug in bugs:
        reporter = db.get(models.User, bug.reporter_id)
        build = db.get(models.Build, bug.reported_build_id) if bug.reported_build_id else None
        task = db.get(models.Task, bug.task_id) if bug.task_id else None
        evidence_count = db.scalar(select(func.count()).select_from(models.EvidenceAsset).where(
            models.EvidenceAsset.owner_type == "bug_report",
            models.EvidenceAsset.owner_id == bug.id,
        )) or 0
        result.append({
            "id": bug.id,
            "key": bug.public_key,
            "title": bug.title,
            "bug_type": bug.bug_type,
            "trigger": bug.trigger,
            "status": bug.status.value,
            "root_cause": bug.root_cause,
            "reporter_id": bug.reporter_id,
            "reporter_name": reporter.display_name if reporter else "Bilinmiyor",
            "reported_build_id": bug.reported_build_id,
            "build_version": build.version if build else None,
            "task_id": bug.task_id,
            "task_title": task.title if task else None,
            "bug_timestamp_seconds": bug.bug_timestamp_seconds,
            "evidence_count": evidence_count,
            "created_at": bug.created_at,
            "updated_at": bug.updated_at,
        })
    return result


@router.get("/admin/bugs/{bug_id}")
def admin_bug_detail(bug_id: str, db: DB, developer: DeveloperUser) -> dict:
    bug = db.get(models.BugReport, bug_id)
    if bug is None:
        raise HTTPException(status_code=404, detail="Hata raporu bulunamadı.")

    reporter = db.get(models.User, bug.reporter_id)
    build = db.get(models.Build, bug.reported_build_id) if bug.reported_build_id else None
    task = db.get(models.Task, bug.task_id) if bug.task_id else None
    section = db.get(models.Section, bug.section_id) if bug.section_id else None

    events = db.scalars(select(models.BugEvent).where(
        models.BugEvent.bug_report_id == bug.id
    ).order_by(models.BugEvent.created_at.asc())).all()
    event_items: list[dict] = []
    for event in events:
        actor = db.get(models.User, event.actor_user_id)
        event_build = db.get(models.Build, event.build_id) if event.build_id else None
        event_items.append({
            "id": event.id,
            "event_type": event.event_type,
            "actor_name": actor.display_name if actor else "Bilinmiyor",
            "previous_status": event.previous_status.value if event.previous_status else None,
            "new_status": event.new_status.value if event.new_status else None,
            "build_version": event_build.version if event_build else None,
            "note": event.note,
            "metadata": event.metadata_json,
            "created_at": event.created_at,
        })

    evidence = db.scalars(select(models.EvidenceAsset).where(
        models.EvidenceAsset.owner_type == "bug_report",
        models.EvidenceAsset.owner_id == bug.id,
    ).order_by(models.EvidenceAsset.created_at.asc())).all()

    retest_items: list[dict] = []
    requests = db.scalars(select(models.RetestRequest).where(
        models.RetestRequest.bug_report_id == bug.id
    ).order_by(models.RetestRequest.created_at.desc())).all()
    for request in requests:
        request_build = db.get(models.Build, request.build_id)
        assignee_ids = list(db.scalars(select(models.RetestAssignee.user_id).where(
            models.RetestAssignee.retest_request_id == request.id
        )).all())
        results = db.scalars(select(models.RetestResult).where(
            models.RetestResult.retest_request_id == request.id
        ).order_by(models.RetestResult.created_at.asc())).all()
        retest_items.append({
            "id": request.id,
            "build_id": request.build_id,
            "build_version": request_build.version if request_build else None,
            "note": request.note,
            "deadline_at": request.deadline_at,
            "closed": request.closed,
            "assignees": [
                user.display_name for user_id in assignee_ids
                if (user := db.get(models.User, user_id)) is not None
            ],
            "results": [{
                "tester_name": tester.display_name if (tester := db.get(models.User, item.tester_id)) else "Bilinmiyor",
                "result": item.result.value,
                "build_version": tested_build.version if (tested_build := db.get(models.Build, item.tested_build_id)) else None,
                "comment": item.comment,
                "created_at": item.created_at,
            } for item in results],
            "created_at": request.created_at,
        })

    return {
        "id": bug.id,
        "key": bug.public_key,
        "project_id": bug.project_id,
        "section_id": bug.section_id,
        "section_name": section.name if section else None,
        "task_id": bug.task_id,
        "task_title": task.title if task else None,
        "reported_build_id": bug.reported_build_id,
        "build_version": build.version if build else None,
        "reporter_id": bug.reporter_id,
        "reporter_name": reporter.display_name if reporter else "Bilinmiyor",
        "title": bug.title,
        "bug_type": bug.bug_type,
        "trigger": bug.trigger,
        "description": bug.description,
        "repro_attempts": bug.repro_attempts,
        "repro_hits": bug.repro_hits,
        "bug_timestamp_seconds": bug.bug_timestamp_seconds,
        "status": bug.status.value,
        "root_cause": bug.root_cause,
        "created_at": bug.created_at,
        "updated_at": bug.updated_at,
        "events": event_items,
        "evidence": [{
            "id": asset.id,
            "filename": asset.original_filename,
            "media_type": asset.media_type,
            "size_bytes": asset.size_bytes,
            "sha256": asset.sha256,
            "created_at": asset.created_at,
        } for asset in evidence],
        "retests": retest_items,
    }


@router.get("/admin/projects/{project_id}/builds")
def admin_build_list(project_id: str, db: DB, developer: DeveloperUser) -> list[dict]:
    _project(db, project_id)
    builds = db.scalars(select(models.Build).where(
        models.Build.project_id == project_id
    ).order_by(models.Build.uploaded_at.desc())).all()
    result: list[dict] = []
    for build in builds:
        uploader = db.get(models.User, build.uploaded_by)
        result.append({
            "id": build.id,
            "project_id": build.project_id,
            "version": build.version,
            "title": build.title,
            "description": build.description,
            "installation_instructions": build.installation_instructions,
            "changelog": build.changelog,
            "original_filename": build.original_filename,
            "size_bytes": build.size_bytes,
            "sha256": build.sha256,
            "status": build.status.value,
            "uploaded_by": uploader.display_name if uploader else "Bilinmiyor",
            "uploaded_at": build.uploaded_at,
            "published_at": build.published_at,
            "archived_at": build.archived_at,
        })
    return result


@router.get("/admin/projects/{project_id}/tasks")
def admin_task_list(project_id: str, db: DB, developer: DeveloperUser) -> list[dict]:
    _project(db, project_id)
    tasks = db.scalars(select(models.Task).where(
        models.Task.project_id == project_id
    ).order_by(models.Task.created_at.desc())).all()
    result: list[dict] = []
    for task in tasks:
        build = db.get(models.Build, task.required_build_id) if task.required_build_id else None
        assignee_ids = list(db.scalars(select(models.TaskAssignee.user_id).where(
            models.TaskAssignee.task_id == task.id
        )).all())
        result.append({
            "id": task.id,
            "project_id": task.project_id,
            "section_id": task.section_id,
            "required_build_id": task.required_build_id,
            "build_version": build.version if build else None,
            "title": task.title,
            "description": task.description,
            "deadline_at": task.deadline_at,
            "status": task.status.value,
            "assignee_ids": assignee_ids,
            "assignees": [
                user.display_name for user_id in assignee_ids
                if (user := db.get(models.User, user_id)) is not None
            ],
            "created_at": task.created_at,
        })
    return result


@router.put("/admin/tasks/{task_id}/status", status_code=status.HTTP_204_NO_CONTENT)
def set_task_status(task_id: str, payload: TaskStatusRequest, db: DB, admin: AdminUser) -> None:
    task = db.get(models.Task, task_id)
    if task is None:
        raise HTTPException(status_code=404, detail="Görev bulunamadı.")
    task.status = models.TaskStatus(payload.status)
    db.add(models.AuditEvent(
        project_id=task.project_id,
        actor_user_id=admin.id,
        action="task_status_changed",
        entity_type="task",
        entity_id=task.id,
        payload={"status": task.status.value},
    ))
    db.commit()


@router.get("/admin/projects/{project_id}/members")
def project_members(project_id: str, db: DB, developer: DeveloperUser) -> list[dict]:
    _project(db, project_id)
    memberships = db.scalars(select(models.ProjectMember).where(
        models.ProjectMember.project_id == project_id
    )).all()
    result: list[dict] = []
    for membership in memberships:
        user = db.get(models.User, membership.user_id)
        if user:
            result.append({"id": user.id, "display_name": user.display_name, "role": user.role.value, "enabled": user.enabled})
    return sorted(result, key=lambda item: item["display_name"].casefold())


@router.put("/admin/projects/{project_id}/members", status_code=status.HTTP_204_NO_CONTENT)
def replace_project_members(project_id: str, payload: ProjectMembersRequest, db: DB, admin: AdminUser) -> None:
    project = _project(db, project_id)
    wanted = set(payload.user_ids)
    wanted.add(project.created_by)
    valid = set(db.scalars(select(models.User.id).where(
        models.User.id.in_(wanted), models.User.enabled.is_(True)
    )).all())
    existing = db.scalars(select(models.ProjectMember).where(
        models.ProjectMember.project_id == project_id
    )).all()
    by_user = {item.user_id: item for item in existing}
    for user_id in valid - set(by_user):
        db.add(models.ProjectMember(project_id=project_id, user_id=user_id))
    for user_id, membership in by_user.items():
        if user_id not in wanted and user_id != project.created_by:
            db.delete(membership)
    db.add(models.AuditEvent(
        project_id=project_id,
        actor_user_id=admin.id,
        action="project_members_replaced",
        entity_type="project",
        entity_id=project_id,
        payload={"user_ids": sorted(valid)},
    ))
    db.commit()


@router.get("/admin/projects/{project_id}/sections")
def project_sections(project_id: str, db: DB, developer: DeveloperUser) -> list[dict]:
    _project(db, project_id)
    sections = db.scalars(select(models.Section).where(
        models.Section.project_id == project_id
    ).order_by(models.Section.sort_order.asc(), models.Section.name.asc())).all()
    return [{"id": item.id, "name": item.name, "sort_order": item.sort_order} for item in sections]


@router.post("/admin/projects/{project_id}/sections", status_code=status.HTTP_201_CREATED)
def create_section(project_id: str, payload: SectionCreateRequest, db: DB, admin: AdminUser) -> dict:
    _project(db, project_id)
    section = models.Section(project_id=project_id, name=payload.name.strip(), sort_order=payload.sort_order)
    db.add(section)
    db.flush()
    db.add(models.AuditEvent(
        project_id=project_id,
        actor_user_id=admin.id,
        action="section_created",
        entity_type="section",
        entity_id=section.id,
        payload={"name": section.name},
    ))
    db.commit()
    return {"id": section.id, "name": section.name, "sort_order": section.sort_order}


@router.get("/admin/projects/{project_id}/analytics/drilldown")
def analytics_drilldown(
    project_id: str,
    db: DB,
    developer: DeveloperUser,
    dimension: Literal["status", "bug_type", "reporter", "build"] = "status",
) -> list[dict]:
    _project(db, project_id)
    bugs = db.scalars(select(models.BugReport).where(models.BugReport.project_id == project_id)).all()
    counter: Counter[str] = Counter()
    resolved: Counter[str] = Counter()
    for bug in bugs:
        if dimension == "status":
            key = bug.status.value
        elif dimension == "bug_type":
            key = bug.bug_type
        elif dimension == "reporter":
            user = db.get(models.User, bug.reporter_id)
            key = user.display_name if user else "Bilinmiyor"
        else:
            build = db.get(models.Build, bug.reported_build_id) if bug.reported_build_id else None
            key = build.version if build else "Sürüm belirtilmedi"
        counter[key] += 1
        if bug.status == models.BugStatus.RESOLVED:
            resolved[key] += 1
    return [{
        "key": key,
        "count": count,
        "resolved": resolved[key],
        "resolution_percentage": round((resolved[key] / count * 100) if count else 0, 1),
    } for key, count in counter.most_common()]


@router.get("/admin/projects/{project_id}/retests")
def admin_retests(project_id: str, db: DB, developer: DeveloperUser) -> list[dict]:
    _project(db, project_id)
    requests = db.scalars(
        select(models.RetestRequest)
        .join(models.BugReport, models.BugReport.id == models.RetestRequest.bug_report_id)
        .where(models.BugReport.project_id == project_id)
        .order_by(models.RetestRequest.created_at.desc())
    ).all()
    result: list[dict] = []
    for request in requests:
        bug = db.get(models.BugReport, request.bug_report_id)
        build = db.get(models.Build, request.build_id)
        assignee_ids = list(db.scalars(select(models.RetestAssignee.user_id).where(
            models.RetestAssignee.retest_request_id == request.id
        )).all())
        results = db.scalars(select(models.RetestResult).where(
            models.RetestResult.retest_request_id == request.id
        )).all()
        result.append({
            "id": request.id,
            "bug_id": bug.id if bug else None,
            "bug_key": bug.public_key if bug else "Bilinmiyor",
            "build_version": build.version if build else None,
            "assignees": [
                user.display_name for user_id in assignee_ids
                if (user := db.get(models.User, user_id)) is not None
            ],
            "results": [{
                "tester_name": tester.display_name if (tester := db.get(models.User, item.tester_id)) else "Bilinmiyor",
                "result": item.result.value,
            } for item in results],
            "deadline_at": request.deadline_at,
            "closed": request.closed,
            "created_at": request.created_at,
        })
    return result


@router.get("/admin/storage/info")
def storage_info(developer: DeveloperUser) -> dict:
    return {"mode": "local" if storage.local else "hf", "location": storage.bucket_browser_url()}
