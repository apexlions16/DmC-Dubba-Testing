from __future__ import annotations

import hashlib
from datetime import UTC, datetime

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, Field
from sqlalchemy import func, select

from . import models
from .auth import AdminUser, DB, find_user_by_display_name, issue_device_credential

router = APIRouter(tags=["kimlik-doğrulama"])


class DeviceEnrollmentRequest(BaseModel):
    display_name: str = Field(min_length=2, max_length=120)
    installation_id: str = Field(min_length=16, max_length=200)
    device_name: str | None = Field(default=None, max_length=160)
    client_kind: str = Field(default="tester", pattern="^(tester|admin)$")


class DeviceEnrollmentResponse(BaseModel):
    user_id: str
    display_name: str
    role: str
    device_id: str
    credential: str


class UserCreateRequest(BaseModel):
    display_name: str = Field(min_length=2, max_length=120)
    role: models.UserRole = models.UserRole.TESTER


class UserUpdateRequest(BaseModel):
    enabled: bool


def _installation_hash(installation_id: str) -> str:
    return hashlib.sha256(installation_id.strip().encode("utf-8")).hexdigest()


@router.post(
    "/auth/enroll",
    response_model=DeviceEnrollmentResponse,
    status_code=status.HTTP_201_CREATED,
)
def enroll_device(payload: DeviceEnrollmentRequest, db: DB) -> DeviceEnrollmentResponse:
    user = find_user_by_display_name(db, payload.display_name)
    if user is None:
        raise HTTPException(
            status_code=404,
            detail="Bu adla kayıtlı bir kullanıcı bulunamadı. Lütfen yöneticinizle iletişime geçin.",
        )
    if not user.enabled:
        raise HTTPException(
            status_code=403,
            detail="Bu kullanıcı hesabı devre dışı bırakılmış. Lütfen yöneticinizle iletişime geçin.",
        )
    if payload.client_kind == "admin" and user.role not in {
        models.UserRole.DEVELOPER,
        models.UserRole.ADMIN,
        models.UserRole.SUPER_ADMIN,
    }:
        raise HTTPException(
            status_code=403,
            detail="Bu kullanıcı Yönetim Merkezi uygulamasını kullanma yetkisine sahip değil.",
        )

    fingerprint_hash = _installation_hash(payload.installation_id)
    same_device = db.scalar(
        select(models.Device).where(
            models.Device.user_id == user.id,
            models.Device.fingerprint_hash == fingerprint_hash,
        )
    )

    if same_device is None:
        active_other_device_count = db.scalar(
            select(func.count())
            .select_from(models.Device)
            .where(
                models.Device.user_id == user.id,
                models.Device.revoked.is_(False),
            )
        ) or 0
        if active_other_device_count > 0:
            raise HTTPException(
                status_code=409,
                detail=(
                    "Bu kullanıcı başka bir cihazla eşleştirilmiş. Yeni bir bilgisayarda kullanmak için "
                    "yöneticinin cihaz eşleşmesini sıfırlaması gerekiyor."
                ),
            )

    credential, credential_hash = issue_device_credential()

    if same_device is None:
        device = models.Device(
            user_id=user.id,
            fingerprint_hash=fingerprint_hash,
            credential_hash=credential_hash,
            revoked=False,
            last_seen_at=datetime.now(UTC),
        )
        db.add(device)
        db.flush()
    else:
        same_device.credential_hash = credential_hash
        same_device.revoked = False
        same_device.last_seen_at = datetime.now(UTC)
        device = same_device

    db.add(
        models.AuditEvent(
            actor_user_id=user.id,
            action="device_enrolled",
            entity_type="device",
            entity_id=device.id,
            payload={"device_name": payload.device_name, "client_kind": payload.client_kind},
        )
    )
    db.commit()

    return DeviceEnrollmentResponse(
        user_id=user.id,
        display_name=user.display_name,
        role=user.role.value,
        device_id=device.id,
        credential=credential,
    )


@router.get("/admin/users")
def list_users(db: DB, admin: AdminUser) -> list[dict]:
    users = db.scalars(select(models.User).order_by(models.User.created_at.asc())).all()
    result: list[dict] = []
    for user in users:
        active_devices = db.scalar(
            select(func.count())
            .select_from(models.Device)
            .where(
                models.Device.user_id == user.id,
                models.Device.revoked.is_(False),
            )
        ) or 0
        result.append(
            {
                "id": user.id,
                "display_name": user.display_name,
                "role": user.role.value,
                "enabled": user.enabled,
                "active_devices": active_devices,
                "created_at": user.created_at,
            }
        )
    return result


@router.post("/admin/users", status_code=status.HTTP_201_CREATED)
def create_user(payload: UserCreateRequest, db: DB, admin: AdminUser) -> dict:
    normalized = payload.display_name.strip()
    duplicate = db.scalar(
        select(models.User).where(func.lower(models.User.display_name) == normalized.casefold())
    )
    if duplicate:
        raise HTTPException(status_code=409, detail="Bu adla kayıtlı bir kullanıcı zaten var.")
    if payload.role == models.UserRole.SUPER_ADMIN:
        raise HTTPException(
            status_code=403,
            detail="Süper Yönetici hesabı normal kullanıcı oluşturma ekranından açılamaz.",
        )

    user = models.User(display_name=normalized, role=payload.role)
    db.add(user)
    db.flush()
    db.add(
        models.AuditEvent(
            actor_user_id=admin.id,
            action="user_created",
            entity_type="user",
            entity_id=user.id,
            payload={"display_name": user.display_name, "role": user.role.value},
        )
    )
    db.commit()
    db.refresh(user)
    return {
        "id": user.id,
        "display_name": user.display_name,
        "role": user.role.value,
        "enabled": user.enabled,
        "active_devices": 0,
        "created_at": user.created_at,
    }


@router.put("/admin/users/{user_id}", status_code=status.HTTP_204_NO_CONTENT)
def update_user(user_id: str, payload: UserUpdateRequest, db: DB, admin: AdminUser) -> None:
    user = db.get(models.User, user_id)
    if user is None:
        raise HTTPException(status_code=404, detail="Kullanıcı bulunamadı.")
    if user.id == admin.id and not payload.enabled:
        raise HTTPException(status_code=409, detail="Kendi kullanıcı hesabınızı devre dışı bırakamazsınız.")

    user.enabled = payload.enabled
    db.add(
        models.AuditEvent(
            actor_user_id=admin.id,
            action="user_enabled_changed",
            entity_type="user",
            entity_id=user.id,
            payload={"enabled": payload.enabled},
        )
    )
    db.commit()


@router.post("/admin/users/{user_id}/devices/reset")
def reset_user_devices(user_id: str, db: DB, admin: AdminUser) -> dict:
    user = db.get(models.User, user_id)
    if user is None:
        raise HTTPException(status_code=404, detail="Kullanıcı bulunamadı.")

    devices = db.scalars(select(models.Device).where(models.Device.user_id == user_id)).all()
    revoked = 0
    for device in devices:
        if not device.revoked:
            device.revoked = True
            revoked += 1

    db.add(
        models.AuditEvent(
            actor_user_id=admin.id,
            action="user_devices_reset",
            entity_type="user",
            entity_id=user.id,
            payload={"revoked_device_count": revoked},
        )
    )
    db.commit()
    return {
        "user_id": user.id,
        "display_name": user.display_name,
        "revoked_device_count": revoked,
    }
