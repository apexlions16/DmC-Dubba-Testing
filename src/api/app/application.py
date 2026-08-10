from .build_api import router as build_router
from .main import app
from .release_api import router as release_router

app.include_router(build_router)
app.include_router(release_router)

__all__ = ["app"]
