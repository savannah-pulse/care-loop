# FHIR sample resources (synthetic)

One minimal, valid FHIR R4 resource per resource type CareLoop exchanges: across the FHIR boundary, and
(`CommunicationRequest`, `Communication`) with the patient-channel simulator through the simulator notification provider.
Every file is **synthetic**: identifiers are prefixed `SYN-`, every resource carries the
`simulated` tag, and codes are placeholders rather than clinical codes.

WS3 extends these (or adds variants such as `Task.referral-rejected.json`) as each mapper is built.
`FhirSampleFilesTests` checks that every file here parses as FHIR R4 and is tagged as simulated.
