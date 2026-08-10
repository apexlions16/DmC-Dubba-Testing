from __future__ import annotations

import re
import shutil
from collections.abc import Iterator
from pathlib import Path

from huggingface_hub import HfApi, HfFileSystem, batch_bucket_files

from .db import settings

_SAFE = re.compile(r"[^a-zA-Z0-9._-]+")


def safe_segment(value: str) -> str:
    value = _SAFE.sub("-", value.strip()).strip("-.")
    if not value:
        raise ValueError("Depolama yolu bölümü boş olamaz.")
    return value[:180]


class HfBucketStorage:
    """QA büyük dosya depolama adaptörü.

    Production'da Hugging Face Storage Bucket, paketlenmiş test ortamında ise
    aynı mantıksal klasör yapısıyla yerel disk kullanılır. HF erişim anahtarı
    hiçbir masaüstü istemcisine gitmez.
    """

    def __init__(self, bucket_id: str | None = None, token: str | None = None) -> None:
        self.bucket_id = bucket_id or settings.hf_bucket_id
        self.token = token or settings.hf_token
        self.local = settings.storage_mode.strip().lower() == "local"
        self.local_root = Path(settings.local_storage_root).expanduser().resolve()
        self.api: HfApi | None = None
        self.fs: HfFileSystem | None = None

        if self.local:
            self.local_root.mkdir(parents=True, exist_ok=True)
        else:
            self.api = HfApi(token=self.token)
            self.fs = HfFileSystem(token=self.token)

    def bug_evidence_path(self, project_id: str, report_id: str, asset_id: str, filename: str) -> str:
        return "/".join([
            "projects", safe_segment(project_id), "reports", safe_segment(report_id),
            "evidence", f"{safe_segment(asset_id)}-{safe_segment(Path(filename).name)}",
        ])

    def retest_evidence_path(self, project_id: str, retest_id: str, asset_id: str, filename: str) -> str:
        return "/".join([
            "projects", safe_segment(project_id), "retests", safe_segment(retest_id),
            "evidence", f"{safe_segment(asset_id)}-{safe_segment(Path(filename).name)}",
        ])

    def active_build_path(self, project_id: str, build_id: str, filename: str) -> str:
        return "/".join([
            "projects", safe_segment(project_id), "builds", "active",
            safe_segment(build_id), safe_segment(Path(filename).name),
        ])

    def archived_build_path(self, project_id: str, build_id: str, filename: str) -> str:
        return "/".join([
            "projects", safe_segment(project_id), "builds", "archived",
            safe_segment(build_id), safe_segment(Path(filename).name),
        ])

    def _local_path(self, remote_path: str) -> Path:
        parts = [safe_segment(part) for part in remote_path.replace("\\", "/").split("/") if part]
        result = (self.local_root / Path(*parts)).resolve()
        if result != self.local_root and self.local_root not in result.parents:
            raise ValueError("Geçersiz yerel depolama yolu.")
        return result

    def upload_file(self, local_path: str | Path, remote_path: str) -> None:
        if self.local:
            destination = self._local_path(remote_path)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(local_path), destination)
            return
        batch_bucket_files(self.bucket_id, add=[(str(local_path), remote_path)], token=self.token)

    def copy_file(self, source_path: str, destination_path: str) -> None:
        if self.local:
            source = self._local_path(source_path)
            destination = self._local_path(destination_path)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            return
        assert self.api is not None
        self.api.copy_files(
            f"hf://buckets/{self.bucket_id}/{source_path}",
            f"hf://buckets/{self.bucket_id}/{destination_path}",
        )

    def delete_file(self, remote_path: str) -> None:
        if self.local:
            path = self._local_path(remote_path)
            if path.exists():
                path.unlink()
            return
        batch_bucket_files(self.bucket_id, delete=[remote_path], token=self.token)

    def delete_prefix(self, prefix: str) -> int:
        if not self.local:
            raise RuntimeError(
                "HF üzerinde prefix purge, iki yönetici onaylı üretim silme akışı tamamlanmadan çalıştırılamaz."
            )
        root = self._local_path(prefix)
        if not root.exists():
            return 0
        file_count = sum(1 for path in root.rglob("*") if path.is_file())
        shutil.rmtree(root)
        return file_count

    def archive_build(self, project_id: str, build_id: str, filename: str) -> str:
        source = self.active_build_path(project_id, build_id, filename)
        destination = self.archived_build_path(project_id, build_id, filename)
        self.copy_file(source, destination)
        self.delete_file(source)
        return destination

    def iter_chunks(self, remote_path: str, chunk_size: int = 4 * 1024 * 1024) -> Iterator[bytes]:
        if self.local:
            with self._local_path(remote_path).open("rb") as handle:
                while chunk := handle.read(chunk_size):
                    yield chunk
            return

        assert self.fs is not None
        with self.fs.open(self.hf_uri(remote_path), "rb") as handle:
            while chunk := handle.read(chunk_size):
                yield chunk

    def hf_uri(self, remote_path: str) -> str:
        if self.local:
            return self._local_path(remote_path).as_uri()
        return f"hf://buckets/{self.bucket_id}/{remote_path}"

    def bucket_browser_url(self) -> str:
        if self.local:
            return self.local_root.as_uri()
        return f"https://huggingface.co/buckets/{self.bucket_id}"
