from __future__ import annotations

from contextlib import asynccontextmanager
from datetime import datetime, timezone
from typing import Annotated

from fastapi import Depends, FastAPI, Header, HTTPException, status
from sqlalchemy import func, select, update
from sqlalchemy.orm import Session

from . import models, schemas, services
from .db import Base, engine, get_db, settings


@asynccontextmanager
async def lifespan(_: FastAPI):
    # Initial bootstrap only. Alembic migrations will replace create_all before production.
    Base.metadata.create_all(bind=engine)
    yield


app = FastAPI(title=settings.app_name, version="0.1.0", lifespan=lifespan)
DB = Annotated[Session, Depends(get_db)]


def get_current_user(
    db: DB,
    x_user_id: Annotated[str | None, Header(alias="X-User-Id")] = None,
) -> models.User:
    # Development transport only. Device-bound signed credentials will replace this header.
    if not x_user_id:
        raise HTTPException(status_code=401, detail="Missing user identity")
    user = db.get(models.User, x_user_id)
    if not user or not user.enabled:
        raise HTTPException(status_code=401, detail="Unknown or disabled user")
    return user


CurrentUser = Annotated[models.User, Depends(get_current_user)]


def require_roles(*roles: models.UserRole):
    def dependency(user: CurrentUser) -> models.User:
        if user.role not in roles:
            raise HTTPException(status_code=403, detail="Insufficient role")
        return user

    return dependency


AdminUser = Annotated[
    models.User,
    Depends(require_roles(models.UserRole.ADMIN, models.UserRole.SUPER_ADMIN)),
]
DeveloperUser = Annotated[
    models.User,
    Depends(
        require_roles(
            models.UserRole.DEVELOPER,
            models.UserRole.ADMIN,
            models.UserRole.SUPER_ADMIN,
        )
    ),
]


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "environment": settings.environment}


@app.post("/bootstrap/admin", status_code=status.HTTP_201_CREATED)
def bootstrap_admin(
    db: DB,
    display_name: str,
    x_bootstrap_key: Annotated[str | None, Header(alias="X-Bootstrap-Key")] = None,
) -> dict:
    configured_key = getattr(settings, "bootstrap_key", None)
    if not configured_key or x_bootstrap_key != configured_key:
        raise HTTPException(status_code=403, detail="Bootstrap is disabled or key is invalid")

    if db.scalar(select(func.count()).select_from(models.User)):
        raise HTTPException(status_code=409, detail="Bootstrap can only run on an empty database")

    user = models.User(display_name=display_name, role=models.UserRole.SUPER_ADMIN)
    db.add(user)
    db.commit()
    db.refresh(user)
    return {"id": user.id, "display_name": user.display_name, "role": user.role.value}


@app.get("/me")
def me(user: CurrentUser) -> dict:
    return {"id": user.id, "display_name": user.display_name, "role": user.role.value}


@app.post("/projects", status_code=status.HTTP_201_CREATED)
def create_project(payload: schemas.ProjectCreate, db: DB, admin: AdminUser) -> dict:
    if db.scalar(select(models.Project).where(models.Project.key == payload.key)):
        raise HTTPException(status_code=409, detail="Project key already exists")

    project = models.Project(
        key=payload.key,
        name=payload.name,
        section_label=payload.section_label,
        created_by=admin.id,
    )
    db.add(project)
    db.flush()
    db.add(models.ProjectMember(project_id=project.id, user_id=admin.id))
    db.add(
        models.AuditEvent(
            project_id=project.id,
            actor_user_id=admin.id,
            action="project_created",
            entity_type="project",
            entity_id=project.id,
            payload={"key": project.key, "name": project.name},
        )
    )
    db.commit()
    return {"id": project.id, "key": project.key, "name": project.name, "status": project.status.value}


@app.get("/projects")
def list_projects(db: DB, user: CurrentUser) -> list[dict]:
    if user.role in {models.UserRole.ADMIN, models.UserRole.SUPER_ADMIN}:
        projects = db.scalars(select(models.Project).order_by(models.Project.created_at.desc())).all()
    else:
        projects = db.scalars(
            select(models.Project)
            .join(models.ProjectMember, models.ProjectMember.project_id == models.Project.id)
            .where(models.ProjectMember.user_id == user.id)
            .order_by(models.Project.created_at.desc())
        ).all()
    return [
        {
            "id": p.id,
            "key": p.key,
            "name": p.name,
            "section_label": p.section_label,
            "status": p.status.value,
        }
        for p in projects
    ]


@app.post("/projects/{project_id}/members/{user_id}", status_code=status.HTTP_204_NO_CONTENT)
def add_project_member(project_id: str, user_id: str, db: DB, admin: AdminUser) -> None:
    if not db.get(models.Project, project_id) or not db.get(models.User, user_id):
        raise HTTPException(status_code=404, detail="Project or user not found")
    existing = db.scalar(
        select(models.ProjectMember).where(
            models.ProjectMember.project_id == project_id,
            models.ProjectMember.user_id == user_id,
        )
    )
    if not existing:
        db.add(models.ProjectMember(project_id=project_id, user_id=user_id))
        db.add(
            models.AuditEvent(
                project_id=project_id,
                actor_user_id=admin.id,
                action="project_member_added",
                entity_type="user",
                entity_id=user_id,
            )
        )
        db.commit()


@app.post("/projects/{project_id}/builds", status_code=status.HTTP_201_CREATED)
def create_build(project_id: str, payload: schemas.BuildCreate, db: DB, developer: DeveloperUser) -> dict:
    project = db.get(models.Project, project_id)
    if not project or project.status != models.ProjectStatus.ACTIVE:
        raise HTTPException(status_code=404, detail="Active project not found")

    duplicate = db.scalar(
        select(models.Build).where(
            models.Build.project_id == project_id,
            models.Build.version == payload.version,
        )
    )
    if duplicate:
        raise HTTPException(status_code=409, detail="Build version already exists")

    build = models.Build(
        project_id=project_id,
        version=payload.version,
        title=payload.title,
        description=payload.description,
        installation_instructions=payload.installation_instructions,
        changelog=payload.changelog,
        status=models.BuildStatus.CANDIDATE,
        uploaded_by=developer.id,
    )
    db.add(build)
    db.flush()

    for bug_id in sorted(set(payload.fix_candidate_bug_ids)):
        bug = db.get(models.BugReport, bug_id)
        if not bug or bug.project_id != project_id:
            raise HTTPException(status_code=400, detail=f"Invalid fix candidate: {bug_id}")
        db.add(
            models.BuildFixCandidate(
                build_id=build.id,
                bug_report_id=bug_id,
                added_by=developer.id,
            )
        )

    db.add(
        models.AuditEvent(
            project_id=project_id,
            actor_user_id=developer.id,
            action="build_candidate_created",
            entity_type="build",
            entity_id=build.id,
            payload={"version": build.version, "fix_candidates": payload.fix_candidate_bug_ids},
        )
    )
    db.commit()
    return {"id": build.id, "version": build.version, "status": build.status.value}


@app.post("/projects/{project_id}/builds/{build_id}/publish", status_code=status.HTTP_204_NO_CONTENT)
def publish_build(project_id: str, build_id: str, db: DB, admin: AdminUser) -> None:
    build = db.get(models.Build, build_id)
    if not build or build.project_id != project_id:
        raise HTTPException(status_code=404, detail="Build not found")
    if not build.storage_path:
        raise HTTPException(status_code=409, detail="Build file has not been uploaded")

    now = datetime.now(timezone.utc)
    db.execute(
        update(models.Build)
        .where(
            models.Build.project_id == project_id,
            models.Build.status == models.BuildStatus.CURRENT,
            models.Build.id != build_id,
        )
        .values(status=models.BuildStatus.SUPERSEDED)
    )
    build.status = models.BuildStatus.CURRENT
    build.published_by = admin.id
    build.published_at = now
    db.add(
        models.AuditEvent(
            project_id=project_id,
            actor_user_id=admin.id,
            action="build_published",
            entity_type="build",
            entity_id=build.id,
            payload={"version": build.version},
        )
    )
    db.commit()


@app.get("/projects/{project_id}/builds/current")
def current_build(project_id: str, db: DB, user: CurrentUser) -> dict:
    build = db.scalar(
        select(models.Build).where(
            models.Build.project_id == project_id,
            models.Build.status == models.BuildStatus.CURRENT,
        )
    )
    if not build:
        raise HTTPException(status_code=404, detail="No current test build")
    return {
        "id": build.id,
        "version": build.version,
        "title": build.title,
        "description": build.description,
        "installation_instructions": build.installation_instructions,
        "changelog": build.changelog,
        "size_bytes": build.size_bytes,
        "sha256": build.sha256,
        "published_at": build.published_at,
    }


@app.post("/projects/{project_id}/tasks", status_code=status.HTTP_201_CREATED)
def create_task(project_id: str, payload: schemas.TaskCreate, db: DB, admin: AdminUser) -> dict:
    project = db.get(models.Project, project_id)
    if not project or project.status != models.ProjectStatus.ACTIVE:
        raise HTTPException(status_code=404, detail="Active project not found")

    task = models.Task(
        project_id=project_id,
        section_id=payload.section_id,
        required_build_id=payload.required_build_id,
        title=payload.title,
        description=payload.description,
        deadline_at=payload.deadline_at,
        created_by=admin.id,
    )
    db.add(task)
    db.flush()
    for user_id in sorted(set(payload.assignee_user_ids)):
        db.add(models.TaskAssignee(task_id=task.id, user_id=user_id))
    db.add(
        models.AuditEvent(
            project_id=project_id,
            actor_user_id=admin.id,
            action="shared_task_created",
            entity_type="task",
            entity_id=task.id,
            payload={"assignees": sorted(set(payload.assignee_user_ids)), "deadline": str(payload.deadline_at)},
        )
    )
    db.commit()
    return {"id": task.id, "title": task.title, "assignees": sorted(set(payload.assignee_user_ids))}


@app.get("/tasks/mine")
def my_tasks(db: DB, user: CurrentUser) -> list[dict]:
    tasks = db.scalars(
        select(models.Task)
        .join(models.TaskAssignee, models.TaskAssignee.task_id == models.Task.id)
        .where(models.TaskAssignee.user_id == user.id, models.Task.status == models.TaskStatus.OPEN)
        .order_by(models.Task.deadline_at.asc().nullslast(), models.Task.created_at.desc())
    ).all()
    return [
        {
            "id": t.id,
            "project_id": t.project_id,
            "section_id": t.section_id,
            "required_build_id": t.required_build_id,
            "title": t.title,
            "description": t.description,
            "deadline_at": t.deadline_at,
        }
        for t in tasks
    ]


@app.post("/bugs", status_code=status.HTTP_201_CREATED)
def create_bug(payload: schemas.BugCreate, db: DB, user: CurrentUser) -> dict:
    project = db.get(models.Project, payload.project_id)
    if not project or project.status != models.ProjectStatus.ACTIVE:
        raise HTTPException(status_code=404, detail="Active project not found")

    member = db.scalar(
        select(models.ProjectMember).where(
            models.ProjectMember.project_id == project.id,
            models.ProjectMember.user_id == user.id,
        )
    )
    if not member and user.role not in {models.UserRole.ADMIN, models.UserRole.SUPER_ADMIN}:
        raise HTTPException(status_code=403, detail="User is not assigned to this project")

    if payload.repro_attempts is not None and payload.repro_hits is not None:
        if payload.repro_hits > payload.repro_attempts:
            raise HTTPException(status_code=400, detail="Reproduction hits cannot exceed attempts")

    bug = models.BugReport(
        public_key=services.bug_public_key(project.key),
        project_id=project.id,
        task_id=payload.task_id,
        section_id=payload.section_id,
        reported_build_id=payload.reported_build_id,
        reporter_id=user.id,
        title=payload.title,
        bug_type=payload.bug_type,
        trigger=payload.trigger,
        description=payload.description,
        repro_attempts=payload.repro_attempts,
        repro_hits=payload.repro_hits,
        bug_timestamp_seconds=payload.bug_timestamp_seconds,
    )
    db.add(bug)
    db.flush()
    services.append_bug_event(db, bug, user.id, "report_created", new_status=models.BugStatus.NEW)
    db.commit()
    return {"id": bug.id, "key": bug.public_key, "status": bug.status.value}


@app.post("/bugs/{bug_id}/status", status_code=status.HTTP_204_NO_CONTENT)
def change_bug_status(
    bug_id: str,
    payload: schemas.BugStatusChange,
    db: DB,
    admin: AdminUser,
) -> None:
    bug = db.get(models.BugReport, bug_id)
    if not bug:
        raise HTTPException(status_code=404, detail="Bug not found")
    try:
        services.set_bug_status(
            db,
            bug,
            admin.id,
            payload.status,
            note=payload.note,
            build_id=payload.build_id,
            root_cause=payload.root_cause,
        )
    except ValueError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    db.commit()


@app.post("/bugs/{bug_id}/retests", status_code=status.HTTP_201_CREATED)
def request_retest(
    bug_id: str,
    payload: schemas.RetestCreate,
    db: DB,
    admin: AdminUser,
) -> dict:
    bug = db.get(models.BugReport, bug_id)
    if not bug:
        raise HTTPException(status_code=404, detail="Bug not found")
    try:
        request = services.create_retest_request(
            db,
            bug,
            payload.build_id,
            admin.id,
            payload.assignee_user_ids,
            payload.note,
            payload.deadline_at,
        )
    except ValueError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    db.commit()
    return {"id": request.id, "bug_id": bug.id, "build_id": request.build_id}


@app.post("/retests/{retest_request_id}/result", status_code=status.HTTP_201_CREATED)
def submit_retest(
    retest_request_id: str,
    payload: schemas.RetestSubmit,
    db: DB,
    user: CurrentUser,
) -> dict:
    request = db.get(models.RetestRequest, retest_request_id)
    if not request:
        raise HTTPException(status_code=404, detail="Retest request not found")
    try:
        result = services.submit_retest_result(
            db,
            request,
            user.id,
            payload.tested_build_id,
            payload.result,
            payload.comment,
        )
    except PermissionError as exc:
        raise HTTPException(status_code=403, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    db.commit()
    return {"id": result.id, "result": result.result.value}


@app.get("/projects/{project_id}/analytics/summary", response_model=schemas.AnalyticsSummary)
def project_analytics(project_id: str, db: DB, user: CurrentUser) -> dict:
    return services.analytics_summary(db, project_id)
