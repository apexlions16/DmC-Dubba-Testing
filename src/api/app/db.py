from __future__ import annotations

from collections.abc import Generator

from pydantic_settings import BaseSettings, SettingsConfigDict
from sqlalchemy import create_engine
from sqlalchemy.orm import DeclarativeBase, Session, sessionmaker


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    app_name: str = "Game QA Platform"
    environment: str = "development"
    database_url: str = "sqlite:///./qa-platform.db"
    hf_bucket_id: str = "xykeskin/dmc-turkish-dub-qa-archive"
    hf_token: str | None = None
    bootstrap_key: str | None = None
    device_credential_secret: str | None = None
    max_evidence_upload_bytes: int = 20 * 1024 * 1024 * 1024


settings = Settings()

connect_args = {"check_same_thread": False} if settings.database_url.startswith("sqlite") else {}
engine = create_engine(settings.database_url, pool_pre_ping=True, connect_args=connect_args)
SessionLocal = sessionmaker(bind=engine, autoflush=False, expire_on_commit=False)


class Base(DeclarativeBase):
    pass


def get_db() -> Generator[Session, None, None]:
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
