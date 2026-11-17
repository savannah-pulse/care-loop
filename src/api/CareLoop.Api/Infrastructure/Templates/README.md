# Message templates

Approved message wording lives here, one folder per language (`en/` only for now).

- `{templateKey}.txt` is the standard variant, sent by email. An optional first line `Subject: ...` is the subject.
- `{templateKey}.sms.txt` is the SMS variant, sent by text message. It has no subject.
- Both variants say the same thing for now, and neither carries sensitive content: there is no app or portal where a patient could read more, and a person always gives a result. The two files are kept apart so the wording can differ per channel later.
- Patient templates carry no links. They tell the patient to reply 1 to confirm they got the message, or 2 to ask for a call. The follow-up message goes out before any appointment is booked, so it never asks whether the patient will attend.
- Placeholders are written `{{name}}`.
- Patient appointment templates (`patient-appointment`, and `patient-appointment-place-tbc` when the hospital system named no place) give the date, time and place with the same reply codes.
- Staff templates carry no patient detail: only "a case at {facility} needs your action", the case number to look for in CareLoop (`{{caseReference}}`, as the dashboard shows it; no link) and, on an escalation, who acknowledged the previous level and when, in local time (`{{previousAcknowledgement}}`, empty when nobody did).
- Caregiver templates (`caregiver-appointment`, and `caregiver-appointment-place-tbc` when the hospital system named no place) carry the appointment date, time and place only, never the result.
- Dates and times are local (`Notifications:TimeZone`), written as people read them: "Tuesday 24 November 2026", "13:30". Facility names come from `Notifications:FacilityNames`.

Every template in this folder is **placeholder wording, not approved**.
Owned by WS4.
