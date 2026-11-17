import asyncio

import httpx
import pytest
from pydantic import ValidationError

from careloop_ai_workflow.coordinator import (
    AgentNotConfiguredError,
    InvalidAgentProposalError,
    WorkflowCoordinator,
)
from careloop_ai_workflow.main import app
from careloop_ai_workflow.models import AgentProposal, WorkflowContext
from careloop_ai_workflow.workflow import (
    WORKFLOW_STEPS,
    AgentRole,
    ProposalAction,
    WorkflowStage,
)


async def get_response(path: str) -> httpx.Response:
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=app),
        base_url="http://testserver",
    ) as client:
        return await client.get(path)


def test_health_endpoint_reports_ready() -> None:
    response = asyncio.run(get_response("/health"))

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_stage_endpoint_lists_complete_journey_and_human_gated_closure() -> None:
    response = asyncio.run(get_response("/workflow/stages"))

    assert response.status_code == 200
    stages = response.json()
    assert [stage["stage"] for stage in stages] == [stage.value for stage in WorkflowStage]
    assert stages[-1]["agent_role"] is None
    assert "authorized human" in stages[-1]["responsibility"]
    assert all(stage["human_review_required"] for stage in stages)


def test_workflow_context_rejects_non_synthetic_or_unexpected_fields() -> None:
    base = {
        "case_reference": "case-demo-001",
        "source_event_reference": "event-demo-001",
        "stage": WorkflowStage.RESULT_RECEIVED,
    }

    with pytest.raises(ValidationError):
        WorkflowContext(**base, synthetic=False)

    with pytest.raises(ValidationError):
        WorkflowContext(**base, synthetic=True, clinical_notes="not accepted")


def test_agent_proposal_requires_review_and_cannot_request_case_closure() -> None:
    proposal = {
        "case_reference": "case-demo-001",
        "source_event_references": ("event-demo-001",),
        "agent_role": AgentRole.CASE_COORDINATOR,
        "action": ProposalAction.ADD_FOLLOW_UP_TASK,
    }

    with pytest.raises(ValidationError):
        AgentProposal(**proposal, requires_human_review=False)

    with pytest.raises(ValidationError):
        AgentProposal(**{**proposal, "action": "close_case"})


def test_closure_stage_has_no_agent() -> None:
    closure_step = next(step for step in WORKFLOW_STEPS if step.stage is WorkflowStage.CLOSED)

    assert closure_step.agent_role is None
    assert closure_step.allowed_actions == ()


class FakeAgent:
    def __init__(self, role: AgentRole, action: ProposalAction) -> None:
        self.role = role
        self._action = action

    async def propose(self, context: WorkflowContext) -> AgentProposal:
        return AgentProposal(
            case_reference=context.case_reference,
            source_event_references=(context.source_event_reference,),
            agent_role=self.role,
            action=self._action,
        )


def test_coordinator_routes_stage_and_returns_reviewable_proposal() -> None:
    context = WorkflowContext(
        case_reference="case-demo-001",
        source_event_reference="event-demo-001",
        stage=WorkflowStage.PATIENT_NOTIFIED,
        synthetic=True,
    )
    coordinator = WorkflowCoordinator(
        {
            AgentRole.PATIENT_COMMUNICATION: FakeAgent(
                AgentRole.PATIENT_COMMUNICATION,
                ProposalAction.DRAFT_TEMPLATE_MESSAGE,
            )
        }
    )

    proposal = asyncio.run(coordinator.propose_for_stage(context))

    assert proposal is not None
    assert proposal.requires_human_review is True
    assert proposal.action is ProposalAction.DRAFT_TEMPLATE_MESSAGE


def test_coordinator_rejects_actions_not_allowed_at_the_current_stage() -> None:
    context = WorkflowContext(
        case_reference="case-demo-001",
        source_event_reference="event-demo-001",
        stage=WorkflowStage.PATIENT_NOTIFIED,
        synthetic=True,
    )
    coordinator = WorkflowCoordinator(
        {
            AgentRole.PATIENT_COMMUNICATION: FakeAgent(
                AgentRole.PATIENT_COMMUNICATION,
                ProposalAction.ADD_FOLLOW_UP_TASK,
            )
        }
    )

    with pytest.raises(InvalidAgentProposalError):
        asyncio.run(coordinator.propose_for_stage(context))


def test_coordinator_fails_explicitly_when_stage_agent_is_not_configured() -> None:
    context = WorkflowContext(
        case_reference="case-demo-001",
        source_event_reference="event-demo-001",
        stage=WorkflowStage.RESULT_RECEIVED,
        synthetic=True,
    )

    with pytest.raises(AgentNotConfiguredError):
        asyncio.run(WorkflowCoordinator({}).propose_for_stage(context))


def test_coordinator_never_routes_closed_cases_to_an_agent() -> None:
    context = WorkflowContext(
        case_reference="case-demo-001",
        source_event_reference="event-demo-001",
        stage=WorkflowStage.CLOSED,
        synthetic=True,
    )

    proposal = asyncio.run(WorkflowCoordinator({}).propose_for_stage(context))

    assert proposal is None
