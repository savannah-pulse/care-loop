# CareLoop AI design

This folder contains the AI-layer design set aligned with the CareLoop backend and the v0.3 AI engineering design specification.

| Document | Purpose |
|---|---|
| [AI specification](ai-spec.md) | FR-11 handoff summaries and governed AI extensions, including UAE Arabic translation, aggregate stage-insight narratives and the proposed role briefing (AG7) |
| [Architecture](architecture.md) | System boundaries, components, case lifecycle and the role of AI |
| [Integration contract](integration.md) | API-to-Python handoff draft flow, request/response validation, review, audit and failure handling; and the three Foundry agents on the dashboard: their route, what each is sent, pinned versions, audit, settings and access |
| [Source Word specification](ai-spec.docx) | Shareable Word copy of the supplied v0.3 engineering design specification, including its original figures |

What AI does in CareLoop today, and where each piece is designed:

| Capability | Where staff see it | Who writes it | Designed in | State |
|---|---|---|---|---|
| Clinician handoff summary (AG1, FR-11) | Case, handoff summary | The Python AI workflow service, or a Foundry deployment from the API (`Ai:Provider`) | [ai-spec §2.1](ai-spec.md#21-clinician-handoff-summary), [integration](integration.md) | Designed; stubs to build (WS9) |
| Role briefing (AG7, proposed) | Worklist, "Start here today" | Foundry agent `role-briefing`, version 2 | [ai-spec §2.9](ai-spec.md#29-role-briefing-ag7-proposed-extension), [integration](integration.md#foundry-agents-role-briefing-impact-summary-patient-message-draft) | Built, off by default (`Ai:Agents`) |
| Impact summary (a short AG6 narrative) | Impact, "What the numbers say" | Foundry agent `impact-summary`, version 4 | [ai-spec §2.6, §4.3](ai-spec.md#26-periodic-stage-insights-ag6-extension), [integration](integration.md#foundry-agents-role-briefing-impact-summary-patient-message-draft) | Built, off by default; small numbers hidden. The periodic, reviewed and published narrative is not built |
| Patient message preview (AG3, preview only) | Case, "Message to the patient" | Foundry agent `patient-notification`, version 2 | [ai-spec §2.2](ai-spec.md#22-patient-message-drafting-ag3-p1-stretch), [integration](integration.md#the-patient-safe-step) | Built, off by default; never sent. Whether it is an exception to AG3 is a team decision |

The backend remains authoritative for case state, ownership, deadlines, escalation, metrics, and closure. The AI service and the agents produce drafts only; each capability remains subject to the scope and approvals described in the documents. The AI extensions are proposals, not authorization to implement or enable them.

The Word document is preserved as supplied in substance. Its source file used a legacy Word container despite the `.docx` extension; the repository copy is a valid Office Open XML `.docx` with the original seven figures retained.
