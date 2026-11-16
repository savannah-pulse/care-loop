# Patient inbox

Shows what CareLoop sent the way each channel shows it, following `docs/ui-design` screens 9, 10 and 12, with one tab per channel:

- **Health app and patient portal** (`SmartphoneView`): a smartphone. The home screen has the next step, "I will come" (an acknowledgement and nothing else), "I need another time" and "Call me back" (both ask for a call). After a response, the follow-up screen asks "What will you do next?" and for a 1 to 5 rating; "Send" reports the answer and the rating.
- **Basic phone** (`BasicPhoneView`): a text message on a keypad phone. Key 1 or 2 and press "Reply".
- **Caregiver email** (`EmailView`): a read-only email. Only the patient replies.

Beside the device, `MessageRecords` lists what CareLoop has recorded for the message: delivery, response and confidence, kept apart because delivery is never an acknowledgement. The inbox is re-read every two seconds.

All text comes from `src/locales/en.json` under `inbox.*`. All data shown is simulated.
