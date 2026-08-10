from __future__ import annotations

import re
from pathlib import Path

from huggingface_hub import HfApi, batch_bucket_files

from .db import settings

_SAFE = re.compile(r"[^a-zA-Z0-9._-]+")


def safe_segment(value: str) -> str:
    value = _SAFE.sub("-", value.strip()).strip("-.")
    if not value:
        raise ValueError("Storage path segment cannot be empty")
    return value[:180]


class HfBucketStorage:
    """All write credentials stay server-side.

    The bucket is mutable/non-versioned storage. Database IDs remain authoritative;
    storage paths are derived from immutable project/report/build IDs.
    """

    def __init__(self, bucket_id: str | None = None, token: str | None = None) -> None:
        self.bucket_id = bucket_id or settings.hf_bucket_id
        self.token = token or settings.hf_token
        self.api = HfApi(token=self.token)

    def bug_evidence_path(self, project_id: str, report_id: str, asset_id: str, filename: str) -> str:
        return "/".join(
            [
                "projects",
                safe_segment(project_id),
                "reports",
                safe_segment(report_id),
                "evidence",
                f"{safe_segment(asset_id)}-{safe_segment(Path(filename).name)}",
            ]
        )

    def retest_evidence_path(self, project_id: str, retest_id: str, asset_id: str, filename: str) -> str:
        return "/".join(
            [
                "projects",
                safe_segment(project_id),
                "retests",
                safe_segment(retest_id),
                "evidence",
                f"{safe_segment(asset_id)}-{safe_segment(Path(filename).name)}",
            ]
        )

    def active_build_path(self, project_id: str, build_id: str, filename: str) -> str:
        return "/".join(
            [
                "projects",
                safe_segment(project_id),
                "builds",
                "active",
                safe_segment(build_id),
                safe_segment(Path(filename).name),
            ]
        )

    def archived_build_path(self, project_id: str, build_id: str, filename: str) -> str:
        return "/".join(
            [
                "projects",
                safe_segment(project_id),
                "builds",
                "archived",
                safe_segment(build_id),
                safe_segment(Path(filename).name),
            ]
        )

    def upload_file(self, local_path: str | Path, remote_path: str) -> None:
        batch_bucket_files(
            self.bucket_id,
            add=[(str(local_path), remote_path)],
            token=self.token,
        )

    def copy_file(self, source_path: str, destination_path: str) -> None:
        self.api.copy_files(
            f"hf://buckets/{self.bucket_id}/{source_path}",
            f"hf://buckets/{self.bucket_id}/{destination_path}",
        )

    def delete_file(self, remote_path: str) -> None:
        batch_bucket_files(self.bucket_id, delete=[remote_path], token=self.token)

    def archive_build(self, project_id: str, build_id: str, filename: str) -> str:
        source = self.active_build_path(project_id, build_id, filename)
        destination = self.archived_build_path(project_id, build_id, filename)
        self.copy_file(source, destination)
        self.delete_file(source)
        return destination

    def public_url(self, remote_path: str) -> str:
        # Public buckets can be read without shipping the write token to clients.
        return f"https://huggingface.co/datasets/{self.bucket_id}/resolve/main/{remote_path}"
