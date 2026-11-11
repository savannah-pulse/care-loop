FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1
WORKDIR /app

COPY pyproject.toml README.md ./
COPY careloop_ai_workflow ./careloop_ai_workflow
RUN python -m pip install --no-cache-dir .

RUN useradd --create-home --uid 10001 appuser
USER appuser
EXPOSE 8080
CMD ["uvicorn", "careloop_ai_workflow.main:app", "--host", "0.0.0.0", "--port", "8080"]
