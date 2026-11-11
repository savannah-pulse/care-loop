from enum import StrEnum
from typing import Literal

from pydantic import BaseModel, ConfigDict


class WorkflowStage(StrEnum):
    RESULT_RECEIVED = "RESULT_RECEIVED"
    CASE_CREATED = "CASE_CREATED"
    OWNER_ASSIGNED = "OWNER_ASSIGNED"
    PATIENT_NOTIFIED = "PATIENT_NOTIFIED"
    PATIENT_ACKNOWLEDGED = "PATIENT_ACKNOWLEDGED"
    CONSULTATION_PENDING = "CONSULTATION_PENDING"
    FOLLOW_UP_IN_PROGRESS = "FOLLOW_UP_IN_PROGRESS"
    CARE_OUTCOME_DOCUMENTED = "CARE_OUTCOME_DOCUMENTED"
    CLOSURE_REVIEW = "CLOSURE_REVIEW"
    CLOSED = "CLOSED"


class AgentRole(StrEnum):
    EVENT_INTAKE = "event_intake"
    CASE_COORDINATOR = "case_coordinator"
    PATIENT_COMMUNICATION = "patient_communication"
    PATIENT_SUPPORT = "patient_support"
    HANDOFF_COORDINATOR = "handoff_coordinator"
    FOLLOW_UP_COORDINATOR = "follow_up_coordinator"
    OUTCOME_SUMMARIZER = "outcome_summarizer"
    CLOSURE_REVIEWER = "closure_reviewer"


class ProposalAction(StrEnum):
    ADD_FOLLOW_UP_TASK = "add_follow_up_task"
    DRAFT_TEMPLATE_MESSAGE = "draft_template_message"
    PREPARE_HANDOFF_SUMMARY = "prepare_handoff_summary"
    REQUEST_HUMAN_REVIEW = "request_human_review"
    RAISE_CONFIGURED_EXCEPTION = "raise_configured_exception"


class WorkflowStep(BaseModel):
    model_config = ConfigDict(frozen=True)

    stage: WorkflowStage
    agent_role: AgentRole | None
    responsibility: str
    allowed_actions: tuple[ProposalAction, ...] = ()
    human_review_required: Literal[True] = True


WORKFLOW_STEPS = (
    WorkflowStep(
        stage=WorkflowStage.RESULT_RECEIVED,
        agent_role=AgentRole.EVENT_INTAKE,
        responsibility="Check event references and flag missing or conflicting data for review.",
        allowed_actions=(
            ProposalAction.REQUEST_HUMAN_REVIEW,
            ProposalAction.RAISE_CONFIGURED_EXCEPTION,
        ),
    ),
    WorkflowStep(
        stage=WorkflowStage.CASE_CREATED,
        agent_role=AgentRole.CASE_COORDINATOR,
        responsibility="Prepare coordination tasks from the approved pathway configuration.",
        allowed_actions=(ProposalAction.ADD_FOLLOW_UP_TASK, ProposalAction.REQUEST_HUMAN_REVIEW),
    ),
    WorkflowStep(
        stage=WorkflowStage.OWNER_ASSIGNED,
        agent_role=AgentRole.CASE_COORDINATOR,
        responsibility="Propose an accountable owner or approved queue; never transfer ownership.",
        allowed_actions=(ProposalAction.ADD_FOLLOW_UP_TASK, ProposalAction.REQUEST_HUMAN_REVIEW),
    ),
    WorkflowStep(
        stage=WorkflowStage.PATIENT_NOTIFIED,
        agent_role=AgentRole.PATIENT_COMMUNICATION,
        responsibility="Prepare an approved-template draft for human review; never send it.",
        allowed_actions=(
            ProposalAction.DRAFT_TEMPLATE_MESSAGE,
            ProposalAction.REQUEST_HUMAN_REVIEW,
        ),
    ),
    WorkflowStep(
        stage=WorkflowStage.PATIENT_ACKNOWLEDGED,
        agent_role=AgentRole.PATIENT_SUPPORT,
        responsibility=(
            "Route missing acknowledgements and help requests without inferring consent."
        ),
        allowed_actions=(
            ProposalAction.ADD_FOLLOW_UP_TASK,
            ProposalAction.RAISE_CONFIGURED_EXCEPTION,
            ProposalAction.REQUEST_HUMAN_REVIEW,
        ),
    ),
    WorkflowStep(
        stage=WorkflowStage.CONSULTATION_PENDING,
        agent_role=AgentRole.HANDOFF_COORDINATOR,
        responsibility=(
            "Prepare consultation tasks; never select or accept a provider for a clinician."
        ),
        allowed_actions=(
            ProposalAction.ADD_FOLLOW_UP_TASK,
            ProposalAction.PREPARE_HANDOFF_SUMMARY,
            ProposalAction.REQUEST_HUMAN_REVIEW,
        ),
    ),
    WorkflowStep(
        stage=WorkflowStage.FOLLOW_UP_IN_PROGRESS,
        agent_role=AgentRole.FOLLOW_UP_COORDINATOR,
        responsibility="Track referral and appointment obligations and propose follow-up tasks.",
        allowed_actions=(
            ProposalAction.ADD_FOLLOW_UP_TASK,
            ProposalAction.RAISE_CONFIGURED_EXCEPTION,
            ProposalAction.REQUEST_HUMAN_REVIEW,
        ),
    ),
    WorkflowStep(
        stage=WorkflowStage.CARE_OUTCOME_DOCUMENTED,
        agent_role=AgentRole.OUTCOME_SUMMARIZER,
        responsibility="Summarize source-reported outcomes without interpreting clinical data.",
        allowed_actions=(
            ProposalAction.PREPARE_HANDOFF_SUMMARY,
            ProposalAction.REQUEST_HUMAN_REVIEW,
        ),
    ),
    WorkflowStep(
        stage=WorkflowStage.CLOSURE_REVIEW,
        agent_role=AgentRole.CLOSURE_REVIEWER,
        responsibility="Identify missing approved evidence and request human closure review.",
        allowed_actions=(ProposalAction.REQUEST_HUMAN_REVIEW,),
    ),
    WorkflowStep(
        stage=WorkflowStage.CLOSED,
        agent_role=None,
        responsibility="Closure is performed only by an authorized human through the .NET API.",
    ),
)
