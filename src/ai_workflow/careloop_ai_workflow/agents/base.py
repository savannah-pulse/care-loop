from typing import Protocol

from careloop_ai_workflow.models import AgentProposal, WorkflowContext
from careloop_ai_workflow.workflow import AgentRole


class CoordinationAgent(Protocol):
    """Produces a typed coordination proposal for human and API review."""

    role: AgentRole

    async def propose(self, context: WorkflowContext) -> AgentProposal: ...
