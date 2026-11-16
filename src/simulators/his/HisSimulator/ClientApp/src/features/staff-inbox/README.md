# staff-inbox

Staff inbox: shows what CareLoop has sent to simulated hospital staff (escalations, handoff proposals, call requests, handoff summaries, staff notifications).

`StaffInboxPage` polls `/staff-inbox`, filters recipients and opens details with the corresponding raw
FHIR log body, read once by id from `/fhir-log/{id}`. Requests cancel on filter changes/navigation. Reading an item never accepts ownership
or acknowledges a message.
All data shown by this feature is simulated.
