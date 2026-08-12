from __future__ import annotations

from sqlalchemy import func, select

from . import models
from .db import SessionLocal, settings


def ensure_local_test_seed() -> None:
    """Paketlenmiş test ortamını sıfır kurulum kullanılabilir hale getirir."""
    if not settings.local_test_mode:
        return

    with SessionLocal() as db:
        admin = db.scalar(
            select(models.User).where(
                func.lower(models.User.display_name)
                == settings.local_test_admin_name.strip().lower()
            )
        )
        if admin is None:
            admin = models.User(
                display_name=settings.local_test_admin_name.strip(),
                role=models.UserRole.SUPER_ADMIN,
            )
            db.add(admin)
            db.flush()

        tester = db.scalar(
            select(models.User).where(
                func.lower(models.User.display_name)
                == settings.local_test_tester_name.strip().lower()
            )
        )
        if tester is None:
            tester = models.User(
                display_name=settings.local_test_tester_name.strip(),
                role=models.UserRole.TESTER,
            )
            db.add(tester)
            db.flush()

        project = db.scalar(
            select(models.Project).where(
                models.Project.key == settings.local_test_project_key
            )
        )
        if project is None:
            project = models.Project(
                key=settings.local_test_project_key,
                name=settings.local_test_project_name,
                section_label="Mission",
                created_by=admin.id,
            )
            db.add(project)
            db.flush()

        for user in (admin, tester):
            membership = db.scalar(
                select(models.ProjectMember).where(
                    models.ProjectMember.project_id == project.id,
                    models.ProjectMember.user_id == user.id,
                )
            )
            if membership is None:
                db.add(
                    models.ProjectMember(
                        project_id=project.id,
                        user_id=user.id,
                    )
                )

        db.commit()
