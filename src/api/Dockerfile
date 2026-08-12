FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PORT=7860

RUN useradd -m -u 1000 user
USER user

ENV HOME=/home/user \
    PATH=/home/user/.local/bin:$PATH

WORKDIR $HOME/app

COPY --chown=user pyproject.toml ./pyproject.toml
COPY --chown=user app ./app

RUN python -m pip install --upgrade pip && \
    python -m pip install .

EXPOSE 7860

CMD ["sh", "-c", "uvicorn app.application:app --host 0.0.0.0 --port ${PORT:-7860} --workers 1"]
