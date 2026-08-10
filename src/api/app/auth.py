from __future__ import annotations

import hashlib
import secrets
from datetime import UTC, datetime
from typing import Annotated

from fastapi import Depends, Header, HTTPException
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from . import models
from .db import get_db, settings

DB = Annotated[Session, Depends(get_db)]


def _credential_hash(credential: str) -> str:
    pepper = settings.device_credential_pepper or "development-only-pepper"
    return hashlib.sha256(f"{pepper}:{credential}".encode()).hexdigest()


def issue_device_credential() -> tuple[str, str]:
    credential = secrets.token_urlsafe(48)
    return credential, _credential_hash(credential)


def get_current_user(
    db: DB,
    authorization: Annotated[str | None, Header()] = None,
    x_user_id: Annotated[str | None, Header(alias="X-User-Id")] = None,
) -> models.User:
    if authorization and authorization.startswith("Device "):
        value = authorization.removeprefix("Device ").strip()
        try:
            device_id, credential = value.split(".", 1)
        except ValueError as exc:
            raise HTTPException(status_code=401, detail="Invalid device credential") from exc

        device = db.get(models.Device, device_id)
        if device is None or device.revoked:
            raise HTTPException(status_code=401, detail="Unknown or revoked device")
        if not secrets.compare_digest(device.credential_hash, _credential_hash(credential)):
            raise HTTPException(status_code=401, detail="Invalid device credential")

        user = db.get(models.User, device.user_id)
        if user is None or not user.enabled:
            raise HTTPException(status_code=401, detail="Unknown or disabled user")

        device.last_seen_at = datetime.now(UTC)
        db.commit()
        return user

    if settings.environment == "development" and x_user_id:
        user = db.get(models.User, x_user_id)
        if user and user.enabled:
            return user

    raise HTTPException(status_code=401, detail="Authentication required")


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


def find_user_by_display_name(db: Session, display_name: str) -> models.User | None:
    normalized = display_name.strip().casefold()
    return db.scalar(
        select(models.User).where(func.lower(models.User.display_name) == normalized.lower())
    )
