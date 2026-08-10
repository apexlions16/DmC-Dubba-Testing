from __future__ import annotations

from fastapi import APIRouter, HTTPException
from sqlalchemy import select

from . import models, schemas, services
from .auth import CurrentUser, DB

router = APIRouter(tags=["yeniden-test"])


@router.get("/retests/mine")
def my_pending_retests(db: DB, user: CurrentUser) -> list[dict]:
    completed_request_ids = select(models.RetestResult.retest_request_id).where(
        models.RetestResult.tester_id == user.id
    )

    requests = db.scalars(
        select(models.RetestRequest)
        .join(
            models.RetestAssignee,
            models.RetestAssignee.retest_request_id == models.RetestRequest.id,
        )
        .where(
            models.RetestAssignee.user_id == user.id,
            models.RetestRequest.closed.is_(False),
            models.RetestRequest.id.not_in(completed_request_ids),
        )
        .order_by(
            models.RetestRequest.deadline_at.asc().nullslast(),
            models.RetestRequest.created_at.asc(),
        )
    ).all()

    result: list[dict] = []
    for request in requests:
        bug = db.get(models.BugReport, request.bug_report_id)
        build = db.get(models.Build, request.build_id)
        if bug is None or build is None:
            continue
        result.append(
            {
                "id": request.id,
                "bug_id": bug.id,
                "bug_key": bug.public_key,
                "build_id": build.id,
                "build_version": build.version,
                "title": bug.title,
                "note": request.note,
                "deadline_at": request.deadline_at,
            }
        )
    return result


@router.post("/retests/{retest_request_id}/submit")
def submit_retest_result(
    retest_request_id: str,
    payload: schemas.RetestSubmit,
    db: DB,
    user: CurrentUser,
) -> dict:
    request = db.get(models.RetestRequest, retest_request_id)
    if request is None:
        raise HTTPException(status_code=404, detail="Yeniden test talebi bulunamadı.")

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
    return {
        "id": result.id,
        "request_id": request.id,
        "result": result.result.value,
    }
