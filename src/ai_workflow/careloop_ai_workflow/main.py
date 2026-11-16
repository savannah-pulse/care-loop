"""FastAPI entry point for CareLoop's stateless AI drafting service."""

from contextlib import asynccontextmanager
from typing import AsyncIterator

from fastapi import FastAPI, HTTPException
from starlette.types import ASGIApp, Message, Receive, Scope, Send

from careloop_ai_workflow.agents.drafting import AgentSuite, InvalidAgentOutputError
from careloop_ai_workflow.config import Settings
from careloop_ai_workflow.contracts import (
    DraftRequest,
    DraftResponse,
    StageInsightsRequest,
    StageInsightsResponse,
)
from careloop_ai_workflow.providers.base import StructuredOutputProvider
from careloop_ai_workflow.providers.errors import (
    AIProviderCallError,
    AIProviderUnavailableError,
)
from careloop_ai_workflow.providers.foundry import create_provider
from careloop_ai_workflow.workflow import WORKFLOW_STEPS, WorkflowStep


class RequestBodyLimitMiddleware:
    """Reject oversized AI requests before FastAPI parses their JSON body."""

    def __init__(self, app: ASGIApp, *, maximum_bytes: int) -> None:
        self.app = app
        self.maximum_bytes = maximum_bytes

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http" or not scope["path"].startswith("/ai/v1/"):
            await self.app(scope, receive, send)
            return

        content_length = next(
            (
                value
                for key, value in scope["headers"]
                if key.lower() == b"content-length"
            ),
            None,
        )
        if content_length is not None:
            try:
                declared_length = int(content_length)
            except ValueError:
                declared_length = 0
            if declared_length > self.maximum_bytes:
                await self._send_too_large(scope, receive, send)
                return

        body_messages: list[Message] = []
        body_size = 0
        while True:
            message = await receive()
            body_messages.append(message)
            if message["type"] == "http.request":
                body_size += len(message.get("body", b""))
                if body_size > self.maximum_bytes:
                    await self._send_too_large(scope, receive, send)
                    return
                if not message.get("more_body", False):
                    break
            elif message["type"] == "http.disconnect":
                break

        async def replay_receive() -> Message:
            if body_messages:
                return body_messages.pop(0)
            return await receive()

        await self.app(scope, replay_receive, send)

    @staticmethod
    async def _send_too_large(scope: Scope, receive: Receive, send: Send) -> None:
        from starlette.responses import JSONResponse

        response = JSONResponse(
            status_code=413,
            content={"detail": "Request body exceeds the configured maximum size."},
        )
        await response(scope, receive, send)


def create_app(
    *,
    settings: Settings | None = None,
    provider: StructuredOutputProvider | None = None,
) -> FastAPI:
    """Create an app with an injectable provider for tests and AI-off deployments."""
    runtime_settings = settings or Settings.from_environment()
    runtime_provider = provider or create_provider(runtime_settings)
    agent_suite = AgentSuite(
        runtime_provider,
        prompt_version=runtime_settings.prompt_version,
    )

    @asynccontextmanager
    async def lifespan(_: FastAPI) -> AsyncIterator[None]:
        yield
        runtime_provider.close()

    app = FastAPI(
        title="CareLoop AI workflow",
        description=(
            "Stateless, purpose-specific AI drafts. The .NET API retains workflow authority; "
            "every generated output requires human review."
        ),
        version="0.2.0",
        lifespan=lifespan,
    )
    app.state.agent_suite = agent_suite
    app.add_middleware(
        RequestBodyLimitMiddleware,
        maximum_bytes=runtime_settings.max_input_bytes,
    )

    @app.get("/health")
    def health() -> dict[str, str]:
        """Report process health without claiming Foundry availability."""
        return {"status": "ok"}

    @app.get("/ai/v1/health")
    def ai_health() -> dict[str, str | bool]:
        """Report provider configuration without exposing credentials or endpoints."""
        return {
            "status": "ok",
            "enabled": agent_suite.enabled,
            "provider": agent_suite.provider_name,
            "deployment": agent_suite.deployment_name,
            "prompt_shields_attested": runtime_settings.prompt_shields_attested,
        }

    @app.get("/workflow/stages", response_model=list[WorkflowStep])
    def workflow_stages() -> list[WorkflowStep]:
        """Expose the informational backend stage map retained by the scaffold."""
        return list(WORKFLOW_STEPS)

    @app.post("/ai/v1/drafts", response_model=DraftResponse)
    async def create_draft(body: DraftRequest) -> DraftResponse:
        """Generate one AG1–AG5 draft for API-owned human review."""
        try:
            return await agent_suite.draft(body)
        except AIProviderUnavailableError as exc:
            raise HTTPException(status_code=503, detail=str(exc)) from exc
        except AIProviderCallError as exc:
            raise HTTPException(
                status_code=502,
                detail="The configured AI provider could not produce a valid response.",
            ) from exc
        except InvalidAgentOutputError as exc:
            raise HTTPException(
                status_code=502,
                detail="The generated draft did not pass CareLoop safety validation.",
            ) from exc

    @app.post("/ai/v1/insights", response_model=StageInsightsResponse)
    async def create_insights(body: StageInsightsRequest) -> StageInsightsResponse:
        """Generate AG6 descriptive text from backend-computed aggregates."""
        try:
            return await agent_suite.insights(body)
        except AIProviderUnavailableError as exc:
            raise HTTPException(status_code=503, detail=str(exc)) from exc
        except AIProviderCallError as exc:
            raise HTTPException(
                status_code=502,
                detail="The configured AI provider could not produce a valid response.",
            ) from exc
        except InvalidAgentOutputError as exc:
            raise HTTPException(
                status_code=502,
                detail="The generated insights did not pass CareLoop safety validation.",
            ) from exc

    return app


app = create_app()
