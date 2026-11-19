# Message templates

Approved message wording lives here, one folder per language (`en/` only for now).

- `{templateKey}.txt` is the standard variant, sent by email. An optional first line `Subject: ...` is the subject.
- `{templateKey}.sms.txt` is the SMS variant, sent by text message. It has no subject.
- Both variants say the same thing for now, and neither carries sensitive content: there is no app or portal where a patient could read more, and a person always gives a result. The two files are kept apart so the wording can differ per channel later.
- Patient templates carry no links. They tell the patient to reply 1 to confirm they got the message, or 2 to ask for a call. The follow-up message goes out before any appointment is booked, so it never asks whether the patient will attend.
- Patient templates follow the approved patient notification templates: `patient-follow-up` is T05 (care-team callback request) and `patient-appointment` is T01 (appointment confirmation). Since 19 November 2026 they are shorter and say what happens next, as the team asked after reviewing the video: the follow-up says the care team would like to talk about the next step in the patient's care, and the appointment message gives the place, date and time and how to change it. Three things differ from the approved text and still need the approver's sign-off: the shorter wording; the reply line, because the reply is how an acknowledgement is recorded; and the `Reference:` line every message ends with.
- Patient templates greet the patient by first name only (`{{firstName}}`: the first given name the hospital system sent, or "patient" when it sent none), so the message reads as written to them, not sent to everyone. They never carry the surname, the result or a link, and a caregiver's message never names the patient.
- Patient templates name the facility (`{{facility}}`) and how to contact it (`{{facilityContact}}`, from `Notifications:FacilityContacts`; "the clinic" when a facility has none configured).
- Placeholders are written `{{name}}`.
- Patient appointment templates (`patient-appointment`, and `patient-appointment-place-tbc` when the hospital system named no place) give the place, date and time, then the care team's contact for a change, with the same reply codes. The place is the one the hospital system named for the appointment, so a message never puts the appointment at the clinic that referred the patient.
- Staff templates carry no patient detail: only "a case at {facility} needs your action", the case number to look for in CareLoop (`{{caseReference}}`, as the dashboard shows it; no link) and, on an escalation, who acknowledged the previous level and when, in local time (`{{previousAcknowledgement}}`, empty when nobody did).
- Caregiver templates (`caregiver-appointment`, and `caregiver-appointment-place-tbc` when the hospital system named no place) carry the appointment date, time and place only, never the result.
- Dates and times are local (`Notifications:TimeZone`), written as people read them: "Tuesday 24 November 2026", "13:30". Facility names come from `Notifications:FacilityNames`.

The staff and caregiver templates are **placeholder wording, not approved**, and say so in the message. The patient templates carry no such marker (see the differences above).
Owned by WS4.
