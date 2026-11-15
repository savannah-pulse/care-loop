# clinician-actions

Clinician actions: explicit human source review, consultation, referral, appointment, finalized outcome,
closure authorization and caregiver consent.

`ClinicianActionsPage` reads source-local `/cases` and posts only to the simulator's `/actions/*`.
Clinical codes are human-entered from the pathway with no invented defaults. Staff roles/facility
and evidence are explicit. An upstream refusal is distinct from a local HTTP/network error.
All data shown by this feature is simulated.
