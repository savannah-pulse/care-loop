# fhir-log

FHIR message log: lists the simulated FHIR messages the simulator sent to CareLoop and received from it, with their HTTP status and JSON body.

`FhirLogPage` polls `/fhir-log?limit=200` (the newest 200 entries), filters direction and source case reference, and displays exact
request/received and response bodies, including refusals and absent responses.
All data shown by this feature is simulated.
