from __future__ import annotations

import os
import sys
from pathlib import Path

import uvicorn


def _application_root() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path.cwd()


def _prepare_local_test_environment() -> None:
    if os.environ.get("GAME_QA_PACKAGED_TEST") != "1":
        return

    data_root = _application_root() / "test-data"
    data_root.mkdir(parents=True, exist_ok=True)
    os.environ.setdefault("ENVIRONMENT", "local-test")
    os.environ.setdefault("DATABASE_URL", f"sqlite:///{(data_root / 'qa-platform.db').as_posix()}")
    os.environ.setdefault("STORAGE_MODE", "local")
    os.environ.setdefault("LOCAL_STORAGE_ROOT", str(data_root / "storage"))
    os.environ.setdefault("LOCAL_TEST_MODE", "true")
    os.environ.setdefault("LOCAL_TEST_ADMIN_NAME", "Yönetici")
    os.environ.setdefault("LOCAL_TEST_TESTER_NAME", "Test Kullanıcısı")
    os.environ.setdefault("DEVICE_CREDENTIAL_SECRET", "local-test-only-change-before-production")


def main() -> None:
    _prepare_local_test_environment()
    # Ortam değerleri ayar modeli oluşturulmadan önce hazırlanmalıdır.
    from app.application import app

    uvicorn.run(
        app,
        host="127.0.0.1",
        port=int(os.environ.get("GAME_QA_PORT", "7860")),
        log_level="info",
    )


if __name__ == "__main__":
    main()
