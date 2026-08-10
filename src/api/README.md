---
title: Game QA Platform API
emoji: 🎮
colorFrom: blue
colorTo: indigo
sdk: docker
app_port: 7860
---

# Game QA Platform API

FastAPI backend for the multi-project QA platform.

Production media storage is configured for the Hugging Face Storage Bucket:

`xykeskin/dmc-turkish-dub-qa-archive`

Required runtime configuration is supplied through Hugging Face Space Variables/Secrets. Never commit `HF_TOKEN`, database credentials, bootstrap keys, or device credential secrets to this repository.

Health endpoints:

- `GET /health`
- `GET /health/storage`

After an administrator session is available, `POST /admin/storage/probe` performs a temporary upload/delete probe to verify real bucket write access without leaving a test object behind.
