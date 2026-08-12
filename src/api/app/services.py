from __future__ import annotations

import uuid
from collections import Counter
from datetime import datetime

from sqlalchemy import select
from sqlalchemy.orm import Session

from . import models


ALLOWED_TRANSITIONS: dict[models.BugStatus, set[models.BugStatus]] = {
    models.BugStatus.NEW: {
        models.BugStatus.ACKNOWLEDGED,
        models.BugStatus.DUPLICATE,
        models.BugStatus.NOT_A_BUG,
    },
    models.BugStatus.ACKNOWLEDGED: {
        models.BugStatus.IN_PROGRESS,
        models.BugStatus.ON_HOLD,
        models.BugStatus.DUPLICATE,
        models.BugStatus.NOT_A_BUG,
        models.BugStatus.WONT_FIX,
    },
    models.BugStatus.IN_PROGRESS: {
        models.BugStatus.RETEST_REQUIRED,
        models.BugStatus.ON_HOLD,
        models.BugStatus.WONT_FIX,
    },
    models.BugStatus.RETEST_REQUIRED: {
        models.BugStatus.IN_PROGRESS,
        models.BugStatus.RESOLVED,
        models.BugStatus.ON_HOLD,
    },
    models.BugStatus.RESOLVED: {models.BugStatus.REOPENED},
    models.BugStatus.REOPENED: {
        models.BugStatus.IN_PROGRESS,
        models.BugStatus.RETEST_REQUIRED,
        models.BugStatus.ON_HOLD,
    },
    models.BugStatus.ON_HOLD: {
        models.BugStatus.ACKNOWLEDGED,
        models.BugStatus.IN_PROGRESS,
        models.BugStatus.WONT_FIX,
    },
    models.BugStatus.DUPLICATE: set(),
    models.BugStatus.NOT_A_BUG: set(),
    models.BugStatus.WONT_FIX: {models.BugStatus.REOPENED},
}


def bug_public_key(project_key: str) -> str:
    return f"{project_key.upper()}-{uuid.uuid4().hex[:8].upper()}"


def append_bug_event(
    db: Session,
    bug: models.BugReport,
    actor_user_id: str,
    event_type: str,
    *,
    previous_status: models.BugStatus | None = None,
    new_status: models.BugStatus | None = None,
    build_id: str | None = None,
    note: str | None = None,
    metadata_json: dict | None = None,
) -> models.BugEvent:
    event = models.BugEvent(
        bug_report_id=bug.id,
        event_type=event_type,
        actor_user_id=actor_user_id,
        previous_status=previous_status,
        new_status=new_status,
        build_id=build_id,
        note=note,
        metadata_json=metadata_json,
    )
    db.add(event)
    return event


def set_bug_status(
    db: Session,
    bug: models.BugReport,
    actor_user_id: str,
    new_status: models.BugStatus,
    *,
    note: str | None = None,
    build_id: str | None = None,
    root_cause: str | None = None,
    force: bool = False,
) -> None:
    previous = bug.status
    if previous == new_status:
        return
    if not force and new_status not in ALLOWED_TRANSITIONS.get(previous, set()):
        raise ValueError(
            f"Bu hata durumu doğrudan değiştirilemez: {previous.value} → {new_status.value}"
        )

    bug.status = new_status
    if root_cause is not None:
        bug.root_cause = root_cause

    append_bug_event(
        db,
        bug,
        actor_user_id,
        "status_changed",
        previous_status=previous,
        new_status=new_status,
        build_id=build_id,
        note=note,
    )


def analytics_summary(db: Session, project_id: str) -> dict:
    statuses = db.scalars(
        select(models.BugReport.status).where(models.BugReport.project_id == project_id)
    ).all()
    counts = Counter(statuses)

    excluded = counts[models.BugStatus.DUPLICATE] + counts[models.BugStatus.NOT_A_BUG]
    valid = len(statuses) - excluded
    resolved = counts[models.BugStatus.RESOLVED]
    in_progress = counts[models.BugStatus.IN_PROGRESS] + counts[models.BugStatus.REOPENED]
    retest = counts[models.BugStatus.RETEST_REQUIRED]
    new_or_unstarted = counts[models.BugStatus.NEW] + counts[models.BugStatus.ACKNOWLEDGED]
    on_hold = counts[models.BugStatus.ON_HOLD] + counts[models.BugStatus.WONT_FIX]

    return {
        "total_reports": len(statuses),
        "valid_known_issues": valid,
        "resolved": resolved,
        "in_progress": in_progress,
        "retest_required": retest,
        "new_or_unstarted": new_or_unstarted,
        "on_hold": on_hold,
        "excluded_duplicate_or_not_bug": excluded,
        "resolution_percentage": round((resolved / valid) * 100, 2) if valid else 0.0,
    }


def create_retest_request(
    db: Session,
    bug: models.BugReport,
    build_id: str,
    requester_id: str,
    assignee_ids: list[str],
    note: str,
    deadline_at: datetime | None,
) -> models.RetestRequest:
    request = models.RetestRequest(
        bug_report_id=bug.id,
        build_id=build_id,
        requested_by=requester_id,
        note=note,
        deadline_at=deadline_at,
    )
    db.add(request)
    db.flush()

    for user_id in sorted(set(assignee_ids)):
        db.add(models.RetestAssignee(retest_request_id=request.id, user_id=user_id))

    set_bug_status(
        db,
        bug,
        requester_id,
        models.BugStatus.RETEST_REQUIRED,
        note=note,
        build_id=build_id,
        force=bug.status not in {models.BugStatus.IN_PROGRESS, models.BugStatus.REOPENED},
    )
    append_bug_event(
        db,
        bug,
        requester_id,
        "retest_requested",
        build_id=build_id,
        note=note,
        metadata_json={"retest_request_id": request.id, "assignee_ids": sorted(set(assignee_ids))},
    )
    return request


def submit_retest_result(
    db: Session,
    request: models.RetestRequest,
    tester_id: str,
    tested_build_id: str,
    result_type: models.RetestResultType,
    comment: str,
) -> models.RetestResult:
    existing = db.scalar(
        select(models.RetestResult).where(
            models.RetestResult.retest_request_id == request.id,
            models.RetestResult.tester_id == tester_id,
        )
    )
    if existing:
        raise ValueError("Bu yeniden test için daha önce sonuç gönderdiniz.")

    assigned = db.scalar(
        select(models.RetestAssignee).where(
            models.RetestAssignee.retest_request_id == request.id,
            models.RetestAssignee.user_id == tester_id,
        )
    )
    if not assigned:
        raise PermissionError("Bu yeniden test size atanmadı.")

    result = models.RetestResult(
        retest_request_id=request.id,
        tester_id=tester_id,
        tested_build_id=tested_build_id,
        result=result_type,
        comment=comment,
    )
    db.add(result)
    db.flush()

    bug = db.get(models.BugReport, request.bug_report_id)
    if bug is None:
        raise ValueError("Yeniden teste bağlı hata raporu bulunamadı.")

    append_bug_event(
        db,
        bug,
        tester_id,
        "retest_result_submitted",
        build_id=tested_build_id,
        note=comment,
        metadata_json={"retest_request_id": request.id, "result": result_type.value},
    )

    # Tek bir başarısız sonuç sorunu yeniden çalışma kuyruğuna döndürmek için yeterlidir.
    # Başarılı sonuçlar hatayı otomatik kapatmaz; son Çözüldü kararı yöneticidedir.
    if result_type == models.RetestResultType.FAILED:
        set_bug_status(
            db,
            bug,
            tester_id,
            models.BugStatus.IN_PROGRESS,
            note="Yeniden test başarısız oldu; sorun tekrar çalışma kuyruğuna alındı.",
            build_id=tested_build_id,
        )

    return result
