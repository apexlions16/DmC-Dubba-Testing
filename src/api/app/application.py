from . import main as main_module
from .auth import get_current_user as get_device_current_user
from .auth_api import router as auth_router
from .build_api import router as build_router
from .evidence_api import router as evidence_router
from .release_api import router as release_router

app = main_module.app

# Geçiş döneminde main.py içindeki eski geliştirme kimliği bağımlılığını değiştirmeden,
# bütün endpointlerde gerçek cihaz credential doğrulamasını devreye alır.
# Development ortamında auth.get_current_user X-User-Id geri dönüşünü korur.
app.dependency_overrides[main_module.get_current_user] = get_device_current_user

app.include_router(auth_router)
app.include_router(build_router)
app.include_router(evidence_router)
app.include_router(release_router)

__all__ = ["app"]
