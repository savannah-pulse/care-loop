from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

from careloop_ai_workflow.workflow import AgentRole, ProposalAction, WorkflowStage


class WorkflowContext(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    case_reference: str = Field(min_length=1)
    source_event_reference: str = Field(min_length=1)
    stage: WorkflowStage
    synthetic: Literal[True]


class AgentProposal(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    case_reference: str = Field(min_length=1)
    source_event_references: tuple[str, ...] = Field(min_length=1)
    agent_role: AgentRole
    action: ProposalAction
    requires_human_review: Literal[True] = True
