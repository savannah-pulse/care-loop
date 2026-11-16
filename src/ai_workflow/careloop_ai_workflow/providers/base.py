from typing import Protocol, TypeVar

from pydantic import BaseModel

OutputModel = TypeVar("OutputModel", bound=BaseModel)


class StructuredOutputProvider(Protocol):
    """Provider-neutral interface for schema-constrained LLM responses."""

    provider_name: str
    deployment_name: str
    enabled: bool

    async def generate(
        self,
        *,
        system_prompt: str,
        payload: dict[str, object],
        output_model: type[OutputModel],
    ) -> OutputModel: ...

    def close(self) -> None: ...
