from .main import app
from .release_api import router as release_router

app.include_router(release_router)

__all__ = ["app"]
