# CareLoop AI design

This folder contains the AI-layer design set aligned with the CareLoop backend and the v0.3 AI engineering design specification.

| Document | Purpose |
|---|---|
| [AI specification](ai-spec.md) | The earlier design proposal: FR-11 handoff summaries and governed AI extensions, including UAE Arabic translation and aggregate stage-insight narratives. Superseded by the integration contract where the two differ |
| [Architecture](architecture.md) | System boundaries, components, case lifecycle and the role of AI |
| [Integration contract](integration.md) | What is built: the API asks each use's own agent in a Microsoft Foundry project for the role briefing, the impact summary and the patient message draft (the last is an approved template from the prompt file, chosen by the API and filled in). What is sent, the prompt files, the Foundry agents, reuse, audit and failure handling; also distinguishes the current use-level routing from proposed v5 role/view prompts |
| [Source Word specification](ai-spec.docx) | Shareable Word copy of the supplied v0.3 engineering design specification, including its original figures |

The backend remains authoritative for case state, ownership, deadlines, escalation, metrics, and closure. AI produces drafts for staff only; there is no separate AI service, and the handoff summary is not built. The v5 document's additional role/view variants and operational recommendations are not assumed to be implemented; the integration contract identifies the current routes, prompts, and limits. Each capability remains subject to the scope and approvals described in the documents. The extensions in the AI specification are proposals, not authorization to implement or enable them.

The Word document is preserved as supplied in substance. Its source file used a legacy Word container despite the `.docx` extension; the repository copy is a valid Office Open XML `.docx` with the original seven figures retained.
