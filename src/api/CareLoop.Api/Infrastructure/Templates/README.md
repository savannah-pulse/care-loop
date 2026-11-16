# Message templates

Approved message wording lives here, one folder per language (`en/` only for now).

- `{templateKey}.txt` is the standard variant, sent by email. An optional first line `Subject: ...` is the subject.
- `{templateKey}.sms.txt` is the SMS variant, sent by text message. It has no subject.
- Both variants say the same thing for now, and neither carries sensitive content: there is no app or portal where a patient could read more, and a person always gives a result. The two files are kept apart so the wording can differ per channel later.
- Patient templates carry no links. They tell the patient to reply 1 to say they will come, or 2 to ask for a call.
- Placeholders are written `{{name}}`.
- Staff templates carry no patient detail: only "a case needs your action", a link into the HIS and, on an escalation, the role that acknowledged the previous level (`{{previousAcknowledgement}}`, empty when nobody did).
- Caregiver templates (`caregiver-appointment`, and `caregiver-appointment-place-tbc` when the hospital system named no place) carry the appointment date, time and place only, never the result.

Every template in this folder is **placeholder wording, not approved**.
Owned by WS4.
