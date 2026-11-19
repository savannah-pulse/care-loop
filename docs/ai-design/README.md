# CareLoop AI design

This folder documents the current CareLoop AI-related code and separates it from proposed capabilities.

| Document | Purpose |
|---|---|
| [AI specification](ai-spec.md) | Current API and Foundry prompt-agent status, plus proposed AI extensions |
| [Architecture](architecture.md) | System boundaries, current AI components, intended routing and case lifecycle |
| [Integration contract](integration.md) | Implemented API seams, current agent clients, the missing integration, and the intended reviewed-draft flow |
| [Source Word specification](ai-spec.docx) | Preserved source copy of the supplied v0.3 engineering design specification; it is not the current implementation contract |

The backend remains authoritative for case state, ownership, deadlines, escalation, metrics, and closure. The API has a clinician-handoff summary seam, but its collector, drafter, endpoints, and external provider are unfinished. The three Python scripts under `src/ai_agents/` connect directly to named Foundry prompt agents as console examples; they are not an API service and are not called by the API. Any role/view-specific agent routing described here is a target flow until that wiring is implemented. Other capabilities remain proposals, not authorization to implement or enable them.

The Word document is preserved as supplied in substance. Its source file used a legacy Word container despite the `.docx` extension; the repository copy is a valid Office Open XML `.docx` with the original seven figures retained. Its contents describe the proposal and may not match the implementation status above.
