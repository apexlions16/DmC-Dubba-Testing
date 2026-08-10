from __future__ import annotations

import enum
import uuid
from datetime import datetime, timezone

from sqlalchemy import Boolean, DateTime, Enum, Float, ForeignKey, Integer, JSON, String, Text, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column, relationship

from .db import Base


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def new_id() -> str:
    return str(uuid.uuid4())


class UserRole(str, enum.Enum):
    TESTER = "tester"
    DEVELOPER = "developer"
    ADMIN = "admin"
    SUPER_ADMIN = "super_admin"


class ProjectStatus(str, enum.Enum):
    ACTIVE = "active"
    CLOSED = "closed"
    PURGE_PENDING = "purge_pending"
    PURGED = "purged"


class BuildStatus(str, enum.Enum):
    UPLOADING = "uploading"
    CANDIDATE = "candidate"
    CURRENT = "current"
    SUPERSEDED = "superseded"
    ARCHIVED = "archived"


class TaskStatus(str, enum.Enum):
    OPEN = "open"
    COMPLETED = "completed"
    CANCELLED = "cancelled"


class BugStatus(str, enum.Enum):
    NEW = "new"
    ACKNOWLEDGED = "acknowledged"
    IN_PROGRESS = "in_progress"
    RETEST_REQUIRED = "retest_required"
    RESOLVED = "resolved"
    ON_HOLD = "on_hold"
    DUPLICATE = "duplicate"
    NOT_A_BUG = "not_a_bug"
    WONT_FIX = "wont_fix"
    REOPENED = "reopened"


class RetestResultType(str, enum.Enum):
    PASSED = "passed"
    FAILED = "failed"
    UNCERTAIN = "uncertain"


class NotificationSeverity(str, enum.Enum):
    INFO = "info"
    WARNING = "warning"
    CRITICAL = "critical"
    MUST_READ = "must_read"


class PurgeStatus(str, enum.Enum):
    REQUESTED = "requested"
    SECOND_ADMIN_APPROVED = "second_admin_approved"
    RUNNING = "running"
    FAILED = "failed"
    COMPLETED = "completed"
    CANCELLED = "cancelled"


class User(Base):
    __tablename__ = "users"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    display_name: Mapped[str] = mapped_column(String(120), index=True)
    role: Mapped[UserRole] = mapped_column(Enum(UserRole, native_enum=False), default=UserRole.TESTER)
    enabled: Mapped[bool] = mapped_column(Boolean, default=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class Device(Base):
    __tablename__ = "devices"
    __table_args__ = (UniqueConstraint("user_id", "fingerprint_hash", name="uq_device_user_fingerprint"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    fingerprint_hash: Mapped[str] = mapped_column(String(128))
    credential_hash: Mapped[str] = mapped_column(String(128))
    revoked: Mapped[bool] = mapped_column(Boolean, default=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
    last_seen_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class Project(Base):
    __tablename__ = "projects"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    key: Mapped[str] = mapped_column(String(40), unique=True, index=True)
    name: Mapped[str] = mapped_column(String(200))
    section_label: Mapped[str] = mapped_column(String(50), default="Mission")
    status: Mapped[ProjectStatus] = mapped_column(Enum(ProjectStatus, native_enum=False), default=ProjectStatus.ACTIVE)
    created_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
    closed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class ProjectMember(Base):
    __tablename__ = "project_members"
    __table_args__ = (UniqueConstraint("project_id", "user_id", name="uq_project_member"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    joined_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class Section(Base):
    __tablename__ = "sections"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    name: Mapped[str] = mapped_column(String(150))
    sort_order: Mapped[int] = mapped_column(Integer, default=0)


class Build(Base):
    __tablename__ = "builds"
    __table_args__ = (UniqueConstraint("project_id", "version", name="uq_project_build_version"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    version: Mapped[str] = mapped_column(String(80))
    title: Mapped[str] = mapped_column(String(200))
    description: Mapped[str] = mapped_column(Text, default="")
    installation_instructions: Mapped[str] = mapped_column(Text, default="")
    changelog: Mapped[str] = mapped_column(Text, default="")
    storage_path: Mapped[str | None] = mapped_column(String(1000), nullable=True)
    original_filename: Mapped[str | None] = mapped_column(String(260), nullable=True)
    sha256: Mapped[str | None] = mapped_column(String(64), nullable=True)
    size_bytes: Mapped[int | None] = mapped_column(Integer, nullable=True)
    status: Mapped[BuildStatus] = mapped_column(Enum(BuildStatus, native_enum=False), default=BuildStatus.UPLOADING)
    uploaded_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    published_by: Mapped[str | None] = mapped_column(ForeignKey("users.id"), nullable=True)
    uploaded_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
    published_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    archived_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class BuildFixCandidate(Base):
    __tablename__ = "build_fix_candidates"
    __table_args__ = (UniqueConstraint("build_id", "bug_report_id", name="uq_build_fix_candidate"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    build_id: Mapped[str] = mapped_column(ForeignKey("builds.id", ondelete="CASCADE"), index=True)
    bug_report_id: Mapped[str] = mapped_column(ForeignKey("bug_reports.id", ondelete="CASCADE"), index=True)
    added_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class Task(Base):
    __tablename__ = "tasks"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    section_id: Mapped[str | None] = mapped_column(ForeignKey("sections.id", ondelete="SET NULL"), nullable=True)
    required_build_id: Mapped[str | None] = mapped_column(ForeignKey("builds.id", ondelete="SET NULL"), nullable=True)
    title: Mapped[str] = mapped_column(String(200))
    description: Mapped[str] = mapped_column(Text, default="")
    deadline_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    status: Mapped[TaskStatus] = mapped_column(Enum(TaskStatus, native_enum=False), default=TaskStatus.OPEN)
    created_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class TaskAssignee(Base):
    __tablename__ = "task_assignees"
    __table_args__ = (UniqueConstraint("task_id", "user_id", name="uq_task_assignee"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    task_id: Mapped[str] = mapped_column(ForeignKey("tasks.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    assigned_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
    acknowledged_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class BugReport(Base):
    __tablename__ = "bug_reports"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    public_key: Mapped[str] = mapped_column(String(80), unique=True, index=True)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    section_id: Mapped[str | None] = mapped_column(ForeignKey("sections.id", ondelete="SET NULL"), nullable=True)
    task_id: Mapped[str | None] = mapped_column(ForeignKey("tasks.id", ondelete="SET NULL"), nullable=True, index=True)
    reported_build_id: Mapped[str | None] = mapped_column(ForeignKey("builds.id", ondelete="SET NULL"), nullable=True, index=True)
    reporter_id: Mapped[str] = mapped_column(ForeignKey("users.id"), index=True)
    title: Mapped[str] = mapped_column(String(240))
    bug_type: Mapped[str] = mapped_column(String(100), index=True)
    trigger: Mapped[str | None] = mapped_column(String(120), nullable=True, index=True)
    description: Mapped[str] = mapped_column(Text)
    repro_attempts: Mapped[int | None] = mapped_column(Integer, nullable=True)
    repro_hits: Mapped[int | None] = mapped_column(Integer, nullable=True)
    bug_timestamp_seconds: Mapped[float | None] = mapped_column(Float, nullable=True)
    status: Mapped[BugStatus] = mapped_column(Enum(BugStatus, native_enum=False), default=BugStatus.NEW, index=True)
    root_cause: Mapped[str | None] = mapped_column(String(160), nullable=True, index=True)
    duplicate_of_id: Mapped[str | None] = mapped_column(ForeignKey("bug_reports.id", ondelete="SET NULL"), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow, index=True)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow, onupdate=utcnow)


class BugEvent(Base):
    __tablename__ = "bug_events"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    bug_report_id: Mapped[str] = mapped_column(ForeignKey("bug_reports.id", ondelete="CASCADE"), index=True)
    event_type: Mapped[str] = mapped_column(String(80), index=True)
    actor_user_id: Mapped[str] = mapped_column(ForeignKey("users.id"), index=True)
    previous_status: Mapped[BugStatus | None] = mapped_column(Enum(BugStatus, native_enum=False), nullable=True)
    new_status: Mapped[BugStatus | None] = mapped_column(Enum(BugStatus, native_enum=False), nullable=True)
    build_id: Mapped[str | None] = mapped_column(ForeignKey("builds.id", ondelete="SET NULL"), nullable=True)
    note: Mapped[str | None] = mapped_column(Text, nullable=True)
    metadata_json: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow, index=True)


class EvidenceAsset(Base):
    __tablename__ = "evidence_assets"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    owner_type: Mapped[str] = mapped_column(String(40), index=True)  # bug_report | retest_result
    owner_id: Mapped[str] = mapped_column(String(36), index=True)
    storage_path: Mapped[str] = mapped_column(String(1000), unique=True)
    original_filename: Mapped[str] = mapped_column(String(260))
    media_type: Mapped[str] = mapped_column(String(100), default="video/mp4")
    size_bytes: Mapped[int | None] = mapped_column(Integer, nullable=True)
    sha256: Mapped[str | None] = mapped_column(String(64), nullable=True)
    uploaded_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class RetestRequest(Base):
    __tablename__ = "retest_requests"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    bug_report_id: Mapped[str] = mapped_column(ForeignKey("bug_reports.id", ondelete="CASCADE"), index=True)
    build_id: Mapped[str] = mapped_column(ForeignKey("builds.id"), index=True)
    requested_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    note: Mapped[str] = mapped_column(Text, default="")
    deadline_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    closed: Mapped[bool] = mapped_column(Boolean, default=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class RetestAssignee(Base):
    __tablename__ = "retest_assignees"
    __table_args__ = (UniqueConstraint("retest_request_id", "user_id", name="uq_retest_assignee"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    retest_request_id: Mapped[str] = mapped_column(ForeignKey("retest_requests.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    assigned_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class RetestResult(Base):
    __tablename__ = "retest_results"
    __table_args__ = (UniqueConstraint("retest_request_id", "tester_id", name="uq_retest_result_tester"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    retest_request_id: Mapped[str] = mapped_column(ForeignKey("retest_requests.id", ondelete="CASCADE"), index=True)
    tester_id: Mapped[str] = mapped_column(ForeignKey("users.id"), index=True)
    tested_build_id: Mapped[str] = mapped_column(ForeignKey("builds.id"), index=True)
    result: Mapped[RetestResultType] = mapped_column(Enum(RetestResultType, native_enum=False))
    comment: Mapped[str] = mapped_column(Text, default="")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class Notification(Base):
    __tablename__ = "notifications"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str | None] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), nullable=True, index=True)
    title: Mapped[str] = mapped_column(String(200))
    body: Mapped[str] = mapped_column(Text)
    severity: Mapped[NotificationSeverity] = mapped_column(Enum(NotificationSeverity, native_enum=False), default=NotificationSeverity.INFO)
    created_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    target_type: Mapped[str] = mapped_column(String(40))  # all | project | task | users | user
    target_payload: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    expires_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class NotificationReceipt(Base):
    __tablename__ = "notification_receipts"
    __table_args__ = (UniqueConstraint("notification_id", "user_id", name="uq_notification_receipt"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    notification_id: Mapped[str] = mapped_column(ForeignKey("notifications.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    delivered_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    read_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    acknowledged_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class ClientRelease(Base):
    __tablename__ = "client_releases"
    __table_args__ = (UniqueConstraint("channel", "version", name="uq_release_channel_version"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    channel: Mapped[str] = mapped_column(String(20), index=True)  # stable | beta
    version: Mapped[str] = mapped_column(String(80))
    title: Mapped[str] = mapped_column(String(200))
    notes: Mapped[str] = mapped_column(Text, default="")
    artifact_url: Mapped[str] = mapped_column(String(1000))
    sha256: Mapped[str] = mapped_column(String(64))
    minimum_version: Mapped[str | None] = mapped_column(String(80), nullable=True)
    mandatory: Mapped[bool] = mapped_column(Boolean, default=False)
    published_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    published_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)


class PurgeRequest(Base):
    __tablename__ = "purge_requests"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str] = mapped_column(ForeignKey("projects.id", ondelete="CASCADE"), index=True)
    requested_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    second_admin_id: Mapped[str | None] = mapped_column(ForeignKey("users.id"), nullable=True)
    status: Mapped[PurgeStatus] = mapped_column(Enum(PurgeStatus, native_enum=False), default=PurgeStatus.REQUESTED)
    confirmation_code_hash: Mapped[str] = mapped_column(String(128))
    delete_storage: Mapped[bool] = mapped_column(Boolean, default=True)
    delete_database: Mapped[bool] = mapped_column(Boolean, default=True)
    delete_audit_tombstone: Mapped[bool] = mapped_column(Boolean, default=False)
    preview_json: Mapped[dict] = mapped_column(JSON, default=dict)
    requested_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
    approved_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    completed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class AuditEvent(Base):
    __tablename__ = "audit_events"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    project_id: Mapped[str | None] = mapped_column(String(36), nullable=True, index=True)
    actor_user_id: Mapped[str | None] = mapped_column(String(36), nullable=True, index=True)
    action: Mapped[str] = mapped_column(String(120), index=True)
    entity_type: Mapped[str] = mapped_column(String(80), index=True)
    entity_id: Mapped[str | None] = mapped_column(String(80), nullable=True, index=True)
    payload: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow, index=True)
