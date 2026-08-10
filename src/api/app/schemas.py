from __future__ import annotations

from datetime import datetime
from typing import Literal

from pydantic import BaseModel, Field

from .models import BugStatus, NotificationSeverity, RetestResultType


class ProjectCreate(BaseModel):
    key: str = Field(min_length=2, max_length=40, pattern=r"^[a-z0-9][a-z0-9-]*$")
    name: str = Field(min_length=2, max_length=200)
    section_label: str = Field(default="Mission", min_length=1, max_length=50)


class BuildCreate(BaseModel):
    version: str = Field(min_length=1, max_length=80)
    title: str = Field(min_length=1, max_length=200)
    description: str = ""
    installation_instructions: str = ""
    changelog: str = ""
    fix_candidate_bug_ids: list[str] = Field(default_factory=list)


class TaskCreate(BaseModel):
    title: str = Field(min_length=1, max_length=200)
    description: str = ""
    section_id: str | None = None
    required_build_id: str | None = None
    deadline_at: datetime | None = None
    assignee_user_ids: list[str] = Field(min_length=1)


class BugCreate(BaseModel):
    project_id: str
    task_id: str | None = None
    section_id: str | None = None
    reported_build_id: str | None = None
    title: str = Field(min_length=3, max_length=240)
    bug_type: str = Field(min_length=2, max_length=100)
    trigger: str | None = Field(default=None, max_length=120)
    description: str = Field(min_length=3)
    repro_attempts: int | None = Field(default=None, ge=0, le=100)
    repro_hits: int | None = Field(default=None, ge=0, le=100)
    bug_timestamp_seconds: float | None = Field(default=None, ge=0)


class BugStatusChange(BaseModel):
    status: BugStatus
    note: str | None = None
    root_cause: str | None = Field(default=None, max_length=160)
    build_id: str | None = None


class RetestCreate(BaseModel):
    build_id: str
    assignee_user_ids: list[str] = Field(min_length=1)
    note: str = ""
    deadline_at: datetime | None = None


class RetestSubmit(BaseModel):
    result: RetestResultType
    tested_build_id: str
    comment: str = ""


class NotificationCreate(BaseModel):
    project_id: str | None = None
    title: str = Field(min_length=1, max_length=200)
    body: str = Field(min_length=1)
    severity: NotificationSeverity = NotificationSeverity.INFO
    target_type: Literal["all", "project", "task", "users", "user"]
    target_payload: dict | None = None
    expires_at: datetime | None = None


class AnalyticsSummary(BaseModel):
    total_reports: int
    valid_known_issues: int
    resolved: int
    in_progress: int
    retest_required: int
    new_or_unstarted: int
    on_hold: int
    excluded_duplicate_or_not_bug: int
    resolution_percentage: float
