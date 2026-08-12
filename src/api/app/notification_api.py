from __future__ import annotations

import secrets
from datetime import UTC, datetime
from typing import Literal

from fastapi import APIRouter, HTTPException, WebSocket, WebSocketDisconnect, status
from pydantic import BaseModel, Field
from sqlalchemy import func, select

from . import models
from .auth import AdminUser, CurrentUser, DB, _credential_hash
from .db import SessionLocal
from .notification_models import InboxNotification, InboxRecipient

router = APIRouter(tags=["bildirimler"])


class NotificationCreateRequest(BaseModel):
    project_id: str | None = None
    title: str = Field(min_length=1, max_length=200)
    body: str = Field(min_length=1, max_length=10000)
    severity: Literal["info", "warning", "critical", "must_read"] = "info"
    target_type: Literal["all", "project", "task", "users", "user"] = "all"
    user_ids: list[str] = Field(default_factory=list)
    task_id: str | None = None
    expires_at: datetime | None = None


class ConnectionManager:
    def __init__(self) -> None:
        self.connections: list[WebSocket] = []

    async def connect(self, websocket: WebSocket) -> None:
        await websocket.accept()
        self.connections.append(websocket)

    def disconnect(self, websocket: WebSocket) -> None:
        if websocket in self.connections:
            self.connections.remove(websocket)

    async def broadcast_refresh(self) -> None:
        dead: list[WebSocket] = []
        for websocket in list(self.connections):
            try:
                await websocket.send_json({"type": "notifications_refresh"})
            except Exception:
                dead.append(websocket)
        for websocket in dead:
            self.disconnect(websocket)


manager = ConnectionManager()


def _recipient_ids(db: DB, payload: NotificationCreateRequest) -> list[str]:
    if payload.target_type == "all":
        return list(db.scalars(select(models.User.id).where(models.User.enabled.is_(True))).all())
    if payload.target_type == "project":
        if not payload.project_id:
            raise HTTPException(status_code=400, detail="Proje hedefli bildirim için proje seçilmelidir.")
        return list(db.scalars(select(models.ProjectMember.user_id).where(
            models.ProjectMember.project_id == payload.project_id
        )).all())
    if payload.target_type == "task":
        if not payload.task_id:
            raise HTTPException(status_code=400, detail="Görev hedefli bildirim için görev seçilmelidir.")
        return list(db.scalars(select(models.TaskAssignee.user_id).where(
            models.TaskAssignee.task_id == payload.task_id
        )).all())
    if payload.target_type in {"users", "user"}:
        if not payload.user_ids:
            raise HTTPException(status_code=400, detail="En az bir kullanıcı seçilmelidir.")
        return payload.user_ids
    return []


@router.post("/admin/notifications", status_code=status.HTTP_201_CREATED)
async def create_notification(payload: NotificationCreateRequest, db: DB, admin: AdminUser) -> dict:
    recipient_ids = sorted(set(_recipient_ids(db, payload)))
    if not recipient_ids:
        raise HTTPException(status_code=409, detail="Bildirimin gönderileceği aktif kullanıcı bulunamadı.")

    notification = InboxNotification(
        project_id=payload.project_id,
        title=payload.title.strip(),
        body=payload.body.strip(),
        severity=payload.severity,
        created_by=admin.id,
        expires_at=payload.expires_at,
    )
    db.add(notification)
    db.flush()
    valid_users = set(db.scalars(select(models.User.id).where(
        models.User.id.in_(recipient_ids), models.User.enabled.is_(True)
    )).all())
    for user_id in sorted(valid_users):
        db.add(InboxRecipient(notification_id=notification.id, user_id=user_id))

    db.add(models.AuditEvent(
        project_id=payload.project_id,
        actor_user_id=admin.id,
        action="notification_sent",
        entity_type="notification",
        entity_id=notification.id,
        payload={"severity": payload.severity, "recipient_count": len(valid_users), "target_type": payload.target_type},
    ))
    db.commit()
    await manager.broadcast_refresh()
    return {"id": notification.id, "recipient_count": len(valid_users), "severity": notification.severity}


@router.get("/notifications/mine")
def my_notifications(db: DB, user: CurrentUser, limit: int = 100) -> list[dict]:
    now = datetime.now(UTC)
    rows = db.execute(
        select(InboxNotification, InboxRecipient)
        .join(InboxRecipient, InboxRecipient.notification_id == InboxNotification.id)
        .where(
            InboxRecipient.user_id == user.id,
            (InboxNotification.expires_at.is_(None)) | (InboxNotification.expires_at > now),
        )
        .order_by(InboxNotification.created_at.desc())
        .limit(max(1, min(limit, 250)))
    ).all()
    return [{
        "id": item.id,
        "project_id": item.project_id,
        "title": item.title,
        "body": item.body,
        "severity": item.severity,
        "requires_acknowledgement": item.severity == "must_read",
        "created_at": item.created_at,
        "expires_at": item.expires_at,
        "read_at": receipt.read_at,
        "acknowledged_at": receipt.acknowledged_at,
    } for item, receipt in rows]


@router.post("/notifications/{notification_id}/read", status_code=status.HTTP_204_NO_CONTENT)
def mark_notification_read(notification_id: str, db: DB, user: CurrentUser) -> None:
    receipt = db.scalar(select(InboxRecipient).where(
        InboxRecipient.notification_id == notification_id,
        InboxRecipient.user_id == user.id,
    ))
    if receipt is None:
        raise HTTPException(status_code=404, detail="Bildirim bulunamadı.")
    if receipt.read_at is None:
        receipt.read_at = datetime.now(UTC)
        db.commit()


@router.post("/notifications/{notification_id}/acknowledge", status_code=status.HTTP_204_NO_CONTENT)
def acknowledge_notification(notification_id: str, db: DB, user: CurrentUser) -> None:
    row = db.execute(
        select(InboxNotification, InboxRecipient)
        .join(InboxRecipient, InboxRecipient.notification_id == InboxNotification.id)
        .where(InboxNotification.id == notification_id, InboxRecipient.user_id == user.id)
    ).first()
    if row is None:
        raise HTTPException(status_code=404, detail="Bildirim bulunamadı.")
    notification, receipt = row
    now = datetime.now(UTC)
    receipt.read_at = receipt.read_at or now
    if notification.severity == "must_read":
        receipt.acknowledged_at = now
    db.commit()


@router.get("/admin/notifications")
def admin_notifications(db: DB, admin: AdminUser, project_id: str | None = None) -> list[dict]:
    query = select(InboxNotification).order_by(InboxNotification.created_at.desc())
    if project_id:
        query = query.where(InboxNotification.project_id == project_id)
    notifications = db.scalars(query.limit(250)).all()
    result: list[dict] = []
    for item in notifications:
        total = db.scalar(select(func.count()).select_from(InboxRecipient).where(
            InboxRecipient.notification_id == item.id
        )) or 0
        read = db.scalar(select(func.count()).select_from(InboxRecipient).where(
            InboxRecipient.notification_id == item.id, InboxRecipient.read_at.is_not(None)
        )) or 0
        acknowledged = db.scalar(select(func.count()).select_from(InboxRecipient).where(
            InboxRecipient.notification_id == item.id, InboxRecipient.acknowledged_at.is_not(None)
        )) or 0
        result.append({
            "id": item.id,
            "project_id": item.project_id,
            "title": item.title,
            "body": item.body,
            "severity": item.severity,
            "created_at": item.created_at,
            "recipient_count": total,
            "read_count": read,
            "acknowledged_count": acknowledged,
        })
    return result


def _websocket_user(websocket: WebSocket) -> models.User | None:
    authorization = websocket.headers.get("authorization")
    if not authorization or not authorization.startswith("Device "):
        return None
    try:
        device_id, credential = authorization.removeprefix("Device ").strip().split(".", 1)
    except ValueError:
        return None
    with SessionLocal() as db:
        device = db.get(models.Device, device_id)
        if device is None or device.revoked:
            return None
        if not secrets.compare_digest(device.credential_hash, _credential_hash(credential)):
            return None
        user = db.get(models.User, device.user_id)
        if user is None or not user.enabled:
            return None
        db.expunge(user)
        return user


@router.websocket("/ws/notifications")
async def notifications_websocket(websocket: WebSocket) -> None:
    user = _websocket_user(websocket)
    if user is None:
        await websocket.close(code=4401)
        return
    await manager.connect(websocket)
    try:
        await websocket.send_json({"type": "connected", "user_id": user.id})
        while True:
            await websocket.receive_text()
    except WebSocketDisconnect:
        manager.disconnect(websocket)
    except Exception:
        manager.disconnect(websocket)
