from __future__ import annotations

from contextlib import asynccontextmanager

from . import main as main_module
from .admin_api import router as admin_router
from .auth import get_current_user as get_device_current_user
from .auth_api import router as auth_router
from .build_api import router as build_router
from .evidence_api import router as evidence_router
from .local_test import ensure_local_test_seed
from .notification_api import router as notification_router
from .release_api import router as release_router
from .retest_api import router as retest_router
from .tester_api import router as tester_router

app = main_module.app

# main.py içindeki geliştirme kimliği bağımlılığını gerçek cihaz credential
# doğrulamasıyla değiştirir. Development ortamında X-User-Id fallback'i korunur.
app.dependency_overrides[main_module.get_current_user] = get_device_current_user

app.include_router(auth_router)
app.include_router(admin_router)
app.include_router(build_router)
app.include_router(evidence_router)
app.include_router(notification_router)
app.include_router(retest_router)
app.include_router(release_router)
app.include_router(tester_router)

_original_lifespan = app.router.lifespan_context


@asynccontextmanager
async def platform_lifespan(application):
    async with _original_lifespan(application):
        ensure_local_test_seed()
        yield


app.router.lifespan_context = platform_lifespan

__all__ = ["app"]
