from __future__ import annotations

from datetime import datetime

from sqlalchemy import DateTime, ForeignKey, String, Text, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from . import models
from .db import Base


class InboxNotification(Base):
    __tablename__ = "inbox_notifications"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=models.new_id)
    project_id: Mapped[str | None] = mapped_column(
        ForeignKey("projects.id", ondelete="CASCADE"), nullable=True, index=True
    )
    title: Mapped[str] = mapped_column(String(200))
    body: Mapped[str] = mapped_column(Text)
    severity: Mapped[str] = mapped_column(String(30), default="info", index=True)
    created_by: Mapped[str] = mapped_column(ForeignKey("users.id"), index=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=models.utcnow, index=True
    )
    expires_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class InboxRecipient(Base):
    __tablename__ = "inbox_recipients"
    __table_args__ = (
        UniqueConstraint("notification_id", "user_id", name="uq_inbox_notification_user"),
    )

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=models.new_id)
    notification_id: Mapped[str] = mapped_column(
        ForeignKey("inbox_notifications.id", ondelete="CASCADE"), index=True
    )
    user_id: Mapped[str] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    delivered_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=models.utcnow)
    read_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    acknowledged_at: Mapped[datetime | None] = mapped_column(
        DateTime(timezone=True), nullable=True
    )
