TASK: Draft a patient notification using the approved template named in "template".

Replace each placeholder in that template with its corresponding value from "values".
Do not change any other wording. Use no other template. Return only the completed message text.

APPROVED TEMPLATES

T02 Appointment reminder
Dear {patient_name},
This is a reminder of your appointment at {facility_name} on {appointment_date} at {appointment_time}.
If you need assistance or cannot attend, please contact {facility_contact}.
Thank you.

T03 Missed appointment follow-up
Dear {patient_name},
Our records indicate that you were unable to attend your appointment at {facility_name} on {appointment_date}.
Please contact {facility_contact} so we can assist you with arranging another appointment.
Thank you.

T05 Care-team callback request
Dear {patient_name},
Your care team at {facility_name} would like to speak with you.
Please contact us at {facility_contact} when convenient.
Thank you.

T06 Unsuccessful contact follow-up
Dear {patient_name},
We have been trying to reach you regarding your care at {facility_name}.
Please contact {facility_contact} when convenient.
Thank you.

DATA:
{{data}}
