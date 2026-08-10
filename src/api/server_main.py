from __future__ import annotations

import os
from pathlib import Path

import uvicorn


def _prepare_local_test_environment() -> None:
    if os.environ.get("GAME_QA_PACKAGED_TEST") != "1":
        return

    data_root = Path.cwd() / "test-data"
    data_root.mkdir(parents=True, exist_ok=True)
    os.environ.setdefault("ENVIRONMENT", "local-test")
    os.environ.setdefault("DATABASE_URL", f"sqlite:///{(data_root / 'qa-platform.db').as_posix()}")
    os.environ.setdefault("STORAGE_MODE", "local")
    os.environ.setdefault("LOCAL_STORAGE_ROOT", str(data_root / "storage"))
    os.environ.setdefault("LOCAL_TEST_MODE", "true")
    os.environ.setdefault("LOCAL_TEST_ADMIN_NAME", "Yönetici")
    os.environ.setdefault("LOCAL_TEST_TESTER_NAME", "Test Kullanıcısı")
    os.environ.setdefault("DEVICE_CREDENTIAL_SECRET", "local-test-only-change-before-production")


if __name__ == "__main__":
    _prepare_local_test_environment()
    uvicorn.run(
        "app.application:app",
        host="127.0.0.1",
        port=int(os.environ.get("GAME_QA_PORT", "7860")),
        log_level="info",
    )
