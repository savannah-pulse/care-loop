from typing import Protocol

from careloop_ai_workflow.models import AgentProposal, WorkflowContext
from careloop_ai_workflow.workflow import AgentRole


class ProposalProvider(Protocol):
    """Optional model adapter returning schema-constrained, reviewable proposals."""

    async def propose(
        self,
        agent_role: AgentRole,
        context: WorkflowContext,
    ) -> AgentProposal: ...
