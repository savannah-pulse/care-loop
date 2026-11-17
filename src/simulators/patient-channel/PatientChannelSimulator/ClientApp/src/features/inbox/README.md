# Inboxes

Shows every text message and email CareLoop sent, following `docs/ui-design` screens 9, 10 and 12. CareLoop reaches patients, caregivers and staff by text message and email only: there is no health app or patient portal.

One tab per inbox: the patient's text messages, the patient's emails, a caregiver's messages and staff notices. A message is shown one of two ways:

- **Text message** (`BasicPhoneView`): a keypad phone. Key a reply and press "Reply".
- **Email** (`EmailView`): a mailbox, with the subject line and a reply box.

A reply goes to CareLoop exactly as typed. CareLoop reads 1 as "I got this message" and 2, or anything else, as a request for a call. Only a message to the patient can be replied to: a caregiver's message and a staff notice are read-only.

Beside the message, `MessageRecords` lists what CareLoop has been told: the delivery outcome and each reply, kept apart because delivery is never an acknowledgement. Whether the patient understood, and how confident they feel, are recorded by a nurse in CareLoop after a call, not here. The inboxes are re-read every two seconds.

All text comes from `src/locales/en.json` under `inbox.*`. All data shown is simulated.
