# Message templates

Approved message wording lives here, one folder per language (`en/` only for now).

- `{templateKey}.txt` is the standard variant. An optional first line `Subject: ...` is the subject.
- `{templateKey}.sms.txt` is the SMS-safe variant: it must carry no sensitive content.
- Placeholders are written `{{name}}`.
- Staff templates carry no patient detail: only "a case needs your action", a link into the HIS and, on an escalation, the role that acknowledged the previous level (`{{previousAcknowledgement}}`, empty when nobody did).
- Caregiver templates (`caregiver-appointment`, and `caregiver-appointment-place-tbc` when the hospital system named no place) carry the appointment date, time and place only, never the result. Their SMS-safe variants leave out the place, because a clinic name can say what the appointment is for.

Every template in this folder is **placeholder wording, not approved**.
Owned by WS4.
