"""Environment-backed configuration for the stateless AI drafting service."""

import os
from dataclasses import dataclass


def _read_bool(name: str, default: bool = False) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    normalized = value.strip().lower()
    if normalized not in {"true", "false"}:
        raise ValueError(f"{name} must be 'true' or 'false'.")
    return normalized == "true"


@dataclass(frozen=True)
class Settings:
    """Validated runtime settings without credentials or patient data."""

    provider_mode: str
    project_endpoint: str
    model_deployment: str
    azure_hosted: bool
    managed_identity_client_id: str
    prompt_shields_attested: bool
    max_input_bytes: int
    timeout_seconds: float
    prompt_version: str

    @classmethod
    def from_environment(cls) -> "Settings":
        """Load and validate settings from the process environment."""
        settings = cls(
            provider_mode=os.getenv("CARELOOP_AI_PROVIDER", "disabled").strip().lower(),
            project_endpoint=os.getenv("AZURE_AI_PROJECT_ENDPOINT", "").strip(),
            model_deployment=os.getenv("AZURE_AI_MODEL_DEPLOYMENT_NAME", "").strip(),
            azure_hosted=_read_bool("CARELOOP_AZURE_HOSTED"),
            managed_identity_client_id=os.getenv("AZURE_CLIENT_ID", "").strip(),
            prompt_shields_attested=_read_bool(
                "CARELOOP_FOUNDRY_PROMPT_SHIELDS_ATTESTED"
            ),
            max_input_bytes=int(os.getenv("CARELOOP_AI_MAX_INPUT_BYTES", "262144")),
            timeout_seconds=float(os.getenv("CARELOOP_AI_TIMEOUT_SECONDS", "45")),
            prompt_version=os.getenv("CARELOOP_AI_PROMPT_VERSION", "careloop-ai-v1").strip(),
        )
        if settings.provider_mode not in {"disabled", "foundry"}:
            raise ValueError("CARELOOP_AI_PROVIDER must be 'disabled' or 'foundry'.")
        if settings.max_input_bytes < 1:
            raise ValueError("CARELOOP_AI_MAX_INPUT_BYTES must be positive.")
        if settings.timeout_seconds <= 0:
            raise ValueError("CARELOOP_AI_TIMEOUT_SECONDS must be positive.")
        if not settings.prompt_version:
            raise ValueError("CARELOOP_AI_PROMPT_VERSION must not be empty.")
        return settings
