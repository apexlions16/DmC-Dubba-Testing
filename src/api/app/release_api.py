from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import select
from sqlalchemy.orm import Session

from . import models
from .db import get_db

router = APIRouter(prefix="/client/releases", tags=["client-releases"])


@router.get("/latest")
def latest_release(
    channel: str = Query(default="stable", pattern="^(stable|beta)$"),
    db: Session = Depends(get_db),
) -> dict:
    release = db.scalar(
        select(models.ClientRelease)
        .where(models.ClientRelease.channel == channel)
        .order_by(models.ClientRelease.published_at.desc())
        .limit(1)
    )
    if release is None:
        raise HTTPException(status_code=404, detail="No release published for this channel")

    return {
        "channel": release.channel,
        "version": release.version,
        "title": release.title,
        "notes": release.notes,
        "artifact_url": release.artifact_url,
        "sha256": release.sha256,
        "minimum_version": release.minimum_version,
        "mandatory": release.mandatory,
        "published_at": release.published_at,
    }


@router.get("")
def release_history(
    channel: str = Query(default="stable", pattern="^(stable|beta)$"),
    db: Session = Depends(get_db),
) -> list[dict]:
    releases = db.scalars(
        select(models.ClientRelease)
        .where(models.ClientRelease.channel == channel)
        .order_by(models.ClientRelease.published_at.desc())
    ).all()
    return [
        {
            "id": release.id,
            "channel": release.channel,
            "version": release.version,
            "title": release.title,
            "notes": release.notes,
            "sha256": release.sha256,
            "mandatory": release.mandatory,
            "published_at": release.published_at,
        }
        for release in releases
    ]
