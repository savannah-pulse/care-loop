# clinician-actions

Clinician actions: explicit human source review, consultation, referral, appointment, finalized outcome,
closure authorization and caregiver consent.

`ClinicianActionsPage` reads source-local `/cases` and posts only to the simulator's `/actions/*`.
Clinical codes are human-entered from the pathway with no invented defaults. Staff roles/facility
and evidence are explicit. An upstream refusal is distinct from a local HTTP/network error.

The case list shows the patient's name and MRN with the case reference, and shows the approved handoff
summary read-only once CareLoop has sent one for the case. Closure takes one of the six closure reasons
as a radio button (sent as its wire code such as `on-surveillance`), a date-only next surveillance date
(sent unchanged as `yyyy-MM-dd`), and is sent only after "I have checked the evidence above" is ticked.
Referral and consultation reasons come from a fixed list (no capacity, wrong service, missing
information). A proposed owner needs a role and facility. A referral can be requested, accepted or
rejected, never completed.
All data shown by this feature is simulated.
