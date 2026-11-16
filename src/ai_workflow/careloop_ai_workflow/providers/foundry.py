"""Microsoft Foundry structured-output provider."""

import asyncio
import json
from typing import TypeVar, cast

from azure.core.exceptions import AzureError
from openai import OpenAIError
from pydantic import BaseModel, ValidationError

from careloop_ai_workflow.config import Settings
from careloop_ai_workflow.providers.base import StructuredOutputProvider
from careloop_ai_workflow.providers.errors import (
    AIProviderCallError,
    AIProviderUnavailableError,
)

OutputModel = TypeVar("OutputModel", bound=BaseModel)


class FoundryStructuredOutputProvider:
    """Calls a configured Foundry deployment using Entra ID and Responses parsing."""

    provider_name = "microsoft_foundry"
    enabled = True

    def __init__(self, settings: Settings) -> None:
        if not settings.project_endpoint or not settings.model_deployment:
            raise ValueError(
                "Foundry mode requires AZURE_AI_PROJECT_ENDPOINT and "
                "AZURE_AI_MODEL_DEPLOYMENT_NAME."
            )
        if not settings.prompt_shields_attested:
            raise ValueError(
                "Foundry mode requires confirmation that document-attack Prompt Shields "
                "are enabled with blocking on the model deployment."
            )

        try:
            from azure.ai.projects import AIProjectClient
            from azure.identity import DefaultAzureCredential, ManagedIdentityCredential
        except ImportError as exc:
            raise RuntimeError(
                "Install the AI workflow project's declared dependencies to use Foundry."
            ) from exc

        self.deployment_name = settings.model_deployment
        self._credential = (
            ManagedIdentityCredential(client_id=settings.managed_identity_client_id or None)
            if settings.azure_hosted
            else DefaultAzureCredential()
        )
        self._project_client = AIProjectClient(
            endpoint=settings.project_endpoint,
            credential=self._credential,
        )
        self._client = self._project_client.get_openai_client(
            timeout=settings.timeout_seconds
        )

    async def generate(
        self,
        *,
        system_prompt: str,
        payload: dict[str, object],
        output_model: type[OutputModel],
    ) -> OutputModel:
        """Return one schema-validated completion or fail explicitly."""
        try:
            response = await asyncio.to_thread(
                self._client.responses.parse,
                model=self.deployment_name,
                input=[
                    {"role": "system", "content": system_prompt},
                    {
                        "role": "user",
                        "content": json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
                    },
                ],
                text_format=output_model,
                store=False,
            )
        except (AzureError, OpenAIError, ValidationError) as exc:
            raise AIProviderCallError("Microsoft Foundry inference failed.") from exc

        parsed = response.output_parsed
        if not isinstance(parsed, output_model):
            raise AIProviderCallError("Microsoft Foundry returned no validated structured output.")
        return cast(OutputModel, parsed)

    def close(self) -> None:
        """Close SDK clients and their credential resources."""
        self._client.close()
        self._project_client.close()
        close_credential = getattr(self._credential, "close", None)
        if close_credential is not None:
            close_credential()


class DisabledStructuredOutputProvider:
    """Explicit AI-off provider that never fabricates a draft."""

    provider_name = "disabled"
    deployment_name = "not-configured"
    enabled = False

    async def generate(
        self,
        *,
        system_prompt: str,
        payload: dict[str, object],
        output_model: type[OutputModel],
    ) -> OutputModel:
        """Reject inference when AI is disabled."""
        del system_prompt, payload, output_model
        raise AIProviderUnavailableError("AI drafting is disabled.")

    def close(self) -> None:
        """Satisfy the provider lifecycle interface; no resources are allocated."""


def create_provider(settings: Settings) -> StructuredOutputProvider:
    """Build the configured provider, keeping Foundry optional for AI-off deployments."""
    if settings.provider_mode == "disabled":
        return DisabledStructuredOutputProvider()
    return FoundryStructuredOutputProvider(settings)
