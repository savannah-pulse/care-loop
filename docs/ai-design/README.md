# CareLoop AI design

This folder contains the AI-layer design set aligned with the CareLoop backend and the v0.3 AI engineering design specification.

| Document | Purpose |
|---|---|
| [AI specification](ai-spec.md) | FR-11 handoff summaries and governed AI extensions, including UAE Arabic translation and aggregate stage-insight narratives |
| [Architecture](architecture.md) | System boundaries, components, case lifecycle and the role of AI |
| [Integration contract](integration.md) | API-to-Python handoff draft flow, request/response validation, review, audit and failure handling |
| [Source Word specification](ai-spec.docx) | Shareable Word copy of the supplied v0.3 engineering design specification, including its original figures |

The backend remains authoritative for case state, ownership, deadlines, escalation, metrics, and closure. The AI service produces drafts only; each capability remains subject to the scope and approvals described in the documents. The AI extensions are proposals, not authorization to implement or enable them.

The Word document is preserved as supplied in substance. Its source file used a legacy Word container despite the `.docx` extension; the repository copy is a valid Office Open XML `.docx` with the original seven figures retained.
