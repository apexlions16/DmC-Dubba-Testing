from __future__ import annotations

import os
import tempfile
from datetime import datetime, timezone
from uuid import uuid4

from fastapi import APIRouter, HTTPException, status
from huggingface_hub import HfFileSystem

from . import models
from .auth import CurrentUser
from .db import settings
from .storage import HfBucketStorage

router = APIRouter(tags=["depolama-sagligi"])
storage = HfBucketStorage()


def _is_admin(user: models.User) -> bool:
    return user.role in {models.UserRole.ADMIN, models.UserRole.SUPER_ADMIN}


@router.get("/health/storage")
def storage_health() -> dict:
    mode = settings.storage_mode.strip().lower()
    if mode == "local":
        return {
            "status": "ok",
            "mode": "local",
            "ready_for_upload": True,
            "bucket_id": None,
            "bucket_reachable": None,
            "write_credentials_configured": False,
        }

    bucket_uri = f"hf://buckets/{settings.hf_bucket_id}"
    reachable = False
    error_code: str | None = None
    try:
        fs = HfFileSystem(token=settings.hf_token)
        reachable = bool(fs.exists(bucket_uri))
    except Exception as exc:  # pragma: no cover - remote service dependent
        error_code = type(exc).__name__

    credentials_configured = bool(settings.hf_token)
    return {
        "status": "ok" if reachable else "degraded",
        "mode": "hf",
        "bucket_id": settings.hf_bucket_id,
        "bucket_uri": bucket_uri,
        "bucket_browser_url": f"https://huggingface.co/buckets/{settings.hf_bucket_id}",
        "bucket_reachable": reachable,
        "write_credentials_configured": credentials_configured,
        "ready_for_upload": reachable and credentials_configured,
        "error_code": error_code,
    }


@router.post("/admin/storage/probe")
def probe_storage_write(user: CurrentUser) -> dict:
    if not _is_admin(user):
        raise HTTPException(status_code=403, detail="Bu işlem yalnızca yöneticilere açıktır.")

    mode = settings.storage_mode.strip().lower()
    if mode != "hf":
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Sunucu şu anda Hugging Face depolama modunda çalışmıyor.",
        )
    if not settings.hf_token:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Sunucuda Hugging Face yazma anahtarı yapılandırılmamış.",
        )

    remote_path = f"_system/connection-probes/{uuid4().hex}.txt"
    temp_path: str | None = None
    uploaded = False
    try:
        with tempfile.NamedTemporaryFile(prefix="qa-hf-probe-", suffix=".txt", delete=False) as temp:
            temp_path = temp.name
            temp.write(
                (
                    "Game QA Platform HF storage probe\n"
                    f"timestamp={datetime.now(timezone.utc).isoformat()}\n"
                ).encode("utf-8")
            )

        storage.upload_file(temp_path, remote_path)
        uploaded = True
        storage.delete_file(remote_path)
        uploaded = False
        return {
            "status": "ok",
            "bucket_id": settings.hf_bucket_id,
            "write_verified": True,
            "delete_verified": True,
        }
    except Exception as exc:  # pragma: no cover - remote service dependent
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=f"Hugging Face bucket yazma testi başarısız ({type(exc).__name__}).",
        ) from exc
    finally:
        if uploaded:
            try:
                storage.delete_file(remote_path)
            except Exception:
                pass
        if temp_path and os.path.exists(temp_path):
            os.remove(temp_path)
