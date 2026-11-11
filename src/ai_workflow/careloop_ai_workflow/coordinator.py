from collections.abc import Mapping

from careloop_ai_workflow.agents.base import CoordinationAgent
from careloop_ai_workflow.models import AgentProposal, WorkflowContext
from careloop_ai_workflow.workflow import WORKFLOW_STEPS, AgentRole, WorkflowStage


class AgentNotConfiguredError(RuntimeError):
    pass


class InvalidAgentProposalError(RuntimeError):
    pass


class WorkflowCoordinator:
    def __init__(self, agents: Mapping[AgentRole, CoordinationAgent]) -> None:
        self._agents = agents

    async def propose_for_stage(self, context: WorkflowContext) -> AgentProposal | None:
        step = next(step for step in WORKFLOW_STEPS if step.stage is context.stage)
        if context.stage is WorkflowStage.CLOSED:
            return None
        if step.agent_role is None:
            raise InvalidAgentProposalError("An active workflow stage must have an agent role.")

        agent = self._agents.get(step.agent_role)
        if agent is None:
            raise AgentNotConfiguredError(f"No agent is configured for {step.agent_role.value}.")

        proposal = await agent.propose(context)
        if proposal.agent_role is not step.agent_role:
            raise InvalidAgentProposalError("The proposal role does not match the workflow stage.")
        if proposal.case_reference != context.case_reference:
            raise InvalidAgentProposalError(
                "The proposal case reference does not match its context."
            )
        if proposal.action not in step.allowed_actions:
            raise InvalidAgentProposalError("The proposed action is not allowed at this stage.")

        return proposal
