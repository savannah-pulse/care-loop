#!/usr/bin/env python3
"""Seeds a CareLoop environment with demonstration cases.

It plays the hospital system: it posts FHIR to CareLoop's /fhir boundary, exactly as the HIS
simulator does, but for the facilities and staff the dashboard's "Signed in as" list knows
(family-health-centre, general-hospital; Dr. Rekha, Dr. Sara, Dr. Layla, Huda, Joy, Dr. Obaid).
The shipped scenarios use SYN-FAC-A, which none of those people belongs to, so their cases never
appear on anyone's worklist.

Every patient, name, identifier and address here is invented. Nothing is copied from a real record.
Patients are reached through the messaging simulator, which is told beforehand how each one reacts
(acknowledges, asks for a call, stays silent, or cannot be reached).

Usage:
    seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase overdue
    seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase ontime

Phases, because CareLoop measures deadlines in real time:
    overdue  cases that are meant to miss a deadline and escalate. Run it while the environment
             uses the demo pathway (deadlines in seconds), then wait a few minutes.
    ontime   cases that are closed or in progress and on time. Run it once the environment uses the
             real pathway (deadlines in hours and days), so they stay that way.
    all      both, for an environment where the distinction does not matter.
    completed  a larger set of journeys that met every deadline and closed on evidence, which is what
             most cases in a working programme look like. Run it under the demo pathway too: a step
             counts as "done by the deadline" only once its deadline has passed, and under the real
             pathway that takes a day.
    calls      the nurse navigator calls six patients whose cases are still open and records teach-back.
             Run it after the ontime phase.
    acknowledge  two of the escalations raised by the overdue cases are acknowledged, each by the role
             it currently sits with. Run it after those cases have escalated.

--only takes case labels (the names in square brackets in the output) to send just those cases.

Python 3.9 or later, standard library only. Safe to run twice: every resource has a fixed
identifier and version, so CareLoop treats a repeat as a duplicate.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import sys
import time
import urllib.error
import urllib.request

B = "https://careloop.invalid/fhir"
PATHWAY = "breast-screening"
SIMULATED_TAG = {"system": f"{B}/CodeSystem/data-origin", "code": "simulated", "display": "SIMULATED - synthetic data"}

# --- The people the dashboard knows (src/web/src/identity/staff.ts) ---------------------------------
FHC = "family-health-centre"
GH = "general-hospital"
REKHA = ("rekha-varghese", "clinician", FHC)
SARA = ("sara-al-hammadi", "clinician", FHC)
LAYLA = ("layla-haddad", "receiving-clinician", GH)
HUDA = ("huda-saleh", "nurse-navigator", FHC)
JOY = ("joy-bautista", "facility-lead", FHC)
OBAID = ("obaid-al-suwaidi", "coordinator", "")
# A screening physician at the general hospital. Not on the dashboard's sign-in list: Dr. Layla, who
# is, receives cases there by accepting a handoff from this person.
YUSUF = ("yusuf-al-mansoori", "clinician", GH)
# The general hospital's nurse navigator, likewise not on the sign-in list.
MONA = ("mona-khalil", "nurse-navigator", GH)


def now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")


def in_days(days: float) -> str:
    return (dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=days)).isoformat(timespec="seconds")


def actor(person: tuple[str, str, str]) -> dict:
    """A Practitioner reference carrying the role and facility CareLoop reads."""
    ref, role, facility = person
    return {
        "reference": f"Practitioner/{ref}",
        "extension": [
            {"url": f"{B}/StructureDefinition/actor-role", "valueCode": role},
            {"url": f"{B}/StructureDefinition/actor-facility", "valueString": facility},
        ],
    }


def coding(system: str, code: str) -> dict:
    return {"coding": [{"system": f"{B}/CodeSystem/{system}", "code": code}]}


class Client:
    """Posts to CareLoop and the messaging simulator, and keeps a tally."""

    def __init__(self, api: str, messaging: str, source: str, dry_run: bool) -> None:
        self.api, self.messaging, self.source, self.dry_run = api.rstrip("/"), messaging.rstrip("/"), source, dry_run
        self.failures: list[str] = []

    def _send(self, method: str, url: str, body: dict | None, headers: dict) -> tuple[int, dict, str]:
        if self.dry_run:
            return 0, {}, ""
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(url, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return response.status, dict(response.headers), response.read().decode()
        except urllib.error.HTTPError as error:
            return error.code, dict(error.headers), error.read().decode()

    def fhir(self, label: str, resource: dict, version: int = 1, put: bool = False, expect: tuple[int, ...] = (200, 201)) -> dict:
        """Sends one FHIR resource. The version is the source event's identity: change it with the state."""
        resource = dict(resource)
        resource["meta"] = {"versionId": str(version), "lastUpdated": now(), "source": self.source, "tag": [SIMULATED_TAG]}
        kind, ident = resource["resourceType"], resource["id"]
        url = f"{self.api}/fhir/{kind}/{ident}" if put else f"{self.api}/fhir/{kind}"
        status, headers, text = self._send("PUT" if put else "POST", url, resource, {"Content-Type": "application/fhir+json"})
        self._report(label, status, text, expect)
        return headers

    def rest(self, label: str, path: str, body: dict, person: tuple[str, str, str], expect: tuple[int, ...] = (200, 201, 204)) -> str:
        """Calls a dashboard endpoint as one of the signed-in staff."""
        status, _, text = self._send("POST", f"{self.api}{path}", body, {"Content-Type": "application/json", **self._identity(person)})
        self._report(label, status, text, expect)
        return text

    @staticmethod
    def _identity(person: tuple[str, str, str]) -> dict:
        ref, role, facility = person
        headers = {"X-Demo-User": ref, "X-Demo-Role": role}
        if facility:
            headers["X-Demo-Facility"] = facility
        return headers

    def get(self, path: str, person: tuple[str, str, str]) -> dict | list | None:
        status, _, text = self._send("GET", f"{self.api}{path}", None, self._identity(person))
        return json.loads(text) if status == 200 and text else None

    def behaviour(self, patient_id: str, delivery: str, response: str, respond_after: str = "00:00:02", failure: str | None = None) -> None:
        """Tells the messaging simulator how this patient reacts to a message."""
        body = {"delivery": delivery, "response": response, "respondAfter": respond_after, "failureReason": failure}
        status, _, text = self._send("PUT", f"{self.messaging}/api/patient-behaviours/{patient_id}", body, {"Content-Type": "application/json"})
        self._report("patient behaviour", status, text, (200, 201, 204))

    def _report(self, label: str, status: int, text: str, expect: tuple[int, ...]) -> None:
        if self.dry_run:
            print(f"      {label}: (dry run)")
            return
        ok = status in expect
        detail = ""
        if not ok or status >= 400:
            try:
                parsed = json.loads(text)
                detail = (parsed.get("issue") or [{}])[0].get("diagnostics") or parsed.get("detail") or parsed.get("title") or text
            except (ValueError, AttributeError):
                detail = text
            detail = " - " + str(detail)[:220]
        print(f"      {label}: {status}{'' if ok else '  UNEXPECTED'}{detail}")
        if not ok:
            self.failures.append(f"{label}: {status}{detail}")


class Case:
    """One screening result and the events that follow it."""

    def __init__(self, client: Client, slug: str, name: str, number: int, clinician: tuple[str, str, str] = REKHA,
                 email: bool = False, interpreter: bool = True) -> None:
        self.c, self.slug, self.name, self.clinician = client, slug, name, clinician
        self.patient = f"SYN-PT-{number}"
        self.mrn = f"SYN-MRN-{number}"
        self.report = f"SYN-RESULT-{number}"
        self.number, self.email, self.interpreter = number, email, interpreter
        self.case_id: str | None = None

    # Shared fragments ---------------------------------------------------------------------------
    def _result(self) -> dict:
        return {"reference": f"DiagnosticReport/{self.report}"}

    def _for(self) -> dict:
        return {"reference": f"Patient/{self.patient}"}

    def _result_extension(self) -> list[dict]:
        return [{"url": f"{B}/StructureDefinition/screening-result", "valueReference": self._result()}]

    # Events -------------------------------------------------------------------------------------
    def result(self) -> "Case":
        """The screening result: opens the case. The interpreter, when named, becomes its owner."""
        telecom = [{"system": "sms", "value": self.patient}]
        if self.email:
            telecom.append({"system": "email", "value": f"SYN-EMAIL-{self.number}"})
        patient = {
            "resourceType": "Patient", "id": self.patient,
            "identifier": [{"system": f"{B}/sid/mrn", "value": self.mrn}],
            "name": [{"use": "official", "text": self.name}],
            "telecom": telecom,
            "communication": [{"language": {"coding": [{"system": "urn:ietf:bcp:47", "code": "en"}]}, "preferred": True}],
        }
        report = {
            "resourceType": "DiagnosticReport", "id": self.report, "contained": [patient], "status": "final",
            "category": [coding("pathway", PATHWAY)],
            "code": {"text": "PLACEHOLDER - synthetic, not a clinical code"},
            "subject": {"reference": f"#{self.patient}"}, "issued": now(),
            "performer": [{"reference": f"Organization/{self.clinician[2]}"}],
        }
        if self.interpreter:
            report["resultsInterpreter"] = [actor(self.clinician)]
        self.c.fhir("screening result", report)
        return self

    def find_case_id(self) -> str | None:
        """CareLoop's own id for this case, found by the patient, as the case's clinician would see it."""
        if self.case_id is None:
            cases = self.c.get("/cases?take=200", self.clinician) or []
            self.case_id = next((c["caseId"] for c in cases if c.get("patientReference") == self.patient), None)
        return self.case_id

    def review(self, urgency: str = "PLACEHOLDER-URGENCY-A") -> "Case":
        """Clinician review with the urgency the clinician set. Starts the notification to the patient."""
        self.c.fhir("clinical review", {
            "resourceType": "Task", "id": f"SYN-REVIEW-{self.number}",
            "extension": [{"url": f"{B}/StructureDefinition/reviewed-urgency", "valueCode": urgency}],
            "status": "completed", "intent": "order", "code": coding("task-code", "clinical-review"),
            "focus": self._result(), "for": self._for(), "lastModified": now(), "owner": actor(self.clinician),
        })
        return self

    def consultation(self, to: tuple[str, str, str] = LAYLA) -> "Case":
        """Asks another clinician to take the case over. Ownership moves only if they accept."""
        request_id, task_id = f"SYN-CONSULT-SR-{self.number}", f"SYN-CONSULT-{self.number}"
        self.c.fhir("consultation request", {
            "resourceType": "ServiceRequest", "id": request_id, "status": "active", "intent": "order",
            "category": [coding("service-request-category", "consultation")], "subject": self._for(),
            "authoredOn": now(), "requester": actor(self.clinician), "reasonReference": [self._result()],
        })
        self.c.fhir("handoff proposed", {
            "resourceType": "Task", "id": task_id, "basedOn": [self._result()], "status": "requested", "intent": "order",
            "code": coding("task-code", "consultation"), "focus": {"reference": f"ServiceRequest/{request_id}"},
            "for": self._for(), "lastModified": now(), "requester": actor(self.clinician), "owner": actor(to),
        })
        return self

    def consultation_decision(self, status: str, by: tuple[str, str, str] = LAYLA) -> "Case":
        """The proposed owner accepts or rejects the handoff. Accepting makes them the owner."""
        self.c.fhir(f"handoff {status}", {
            "resourceType": "Task", "id": f"SYN-CONSULT-{self.number}", "basedOn": [self._result()], "status": status,
            "intent": "order", "code": coding("task-code", "consultation"),
            "focus": {"reference": f"ServiceRequest/SYN-CONSULT-SR-{self.number}"}, "for": self._for(),
            "lastModified": now(), "requester": actor(self.clinician), "owner": actor(by),
        }, version=2, put=True)
        return self

    def referral(self, to_facility: str = GH) -> "Case":
        """Sends a referral to another facility."""
        request_id = f"SYN-REFERRAL-SR-{self.number}"
        self.c.fhir("referral sent", {
            "resourceType": "ServiceRequest", "id": request_id, "status": "active", "intent": "order",
            "category": [coding("service-request-category", "referral")], "subject": self._for(),
            "authoredOn": now(), "requester": actor(self.clinician),
            "performer": [{"reference": f"Organization/{to_facility}"}], "reasonReference": [self._result()],
        })
        return self

    def referral_decision(self, status: str, by: tuple[str, str, str] = LAYLA, reason: str | None = None) -> "Case":
        """The receiving clinician accepts or rejects the referral."""
        task = {
            "resourceType": "Task", "id": f"SYN-REFERRAL-{self.number}", "basedOn": [self._result()], "status": status,
            "intent": "order", "code": coding("task-code", "referral"),
            "focus": {"reference": f"ServiceRequest/SYN-REFERRAL-SR-{self.number}"}, "for": self._for(),
            "lastModified": now(), "requester": actor(by), "owner": actor(by),
        }
        if reason:
            task["statusReason"] = {"text": reason}
        self.c.fhir(f"referral {status}", task)
        return self

    def appointment(self, status: str, version: int, start: str, place: str = "General Hospital, Breast Clinic") -> "Case":
        """booked, then fulfilled or noshow. The same appointment each time, with a new version."""
        self.c.fhir(f"appointment {status}", {
            "resourceType": "Appointment", "id": f"SYN-APPT-{self.number}", "extension": self._result_extension(),
            "status": status, "start": start, "created": now(),
            "participant": [
                {"actor": self._for(), "status": "accepted"},
                {"actor": {"reference": f"Location/SYN-LOC-{self.number}", "display": place}, "status": "accepted"},
            ],
        }, version=version, put=version > 1)
        return self

    def outcome(self, kind: str = "diagnosis", by: tuple[str, str, str] = LAYLA, part: str = "") -> "Case":
        """Outcome evidence recorded at the source. CareLoop keeps the reference, never the content."""
        self.c.fhir(f"outcome ({kind})", {
            "resourceType": "Procedure", "id": f"SYN-OUTCOME-{self.number}{part}", "status": "completed",
            "code": {"coding": [{"system": f"{B}/CodeSystem/evidence-kind", "code": kind}], "text": "PLACEHOLDER - synthetic, not a clinical code"},
            "subject": self._for(), "performedDateTime": now(), "performer": [{"actor": actor(by)}],
            "reasonReference": [self._result()],
        })
        return self

    def closure(self, reason: str, by: tuple[str, str, str] = LAYLA, evidence: bool = True,
                next_surveillance: str | None = None, expect: tuple[int, ...] = (200, 201), part: str = "") -> "Case":
        """A person authorizes closure. Without the evidence its reason needs, CareLoop refuses it."""
        outputs = []
        if evidence:
            outputs.append({"type": coding("closure-output", "evidence"), "valueReference": {"reference": f"Procedure/SYN-OUTCOME-{self.number}{part}"}})
        if next_surveillance:
            outputs.append({"type": coding("closure-output", "next-surveillance-date"), "valueDate": next_surveillance})
        task_id = f"SYN-CASE-TASK-{self.number}"
        task = {
            "resourceType": "Task", "id": task_id, "status": "completed", "statusReason": coding("status-reason", reason),
            "intent": "order", "code": coding("task-code", "case"), "basedOn": [self._result()], "for": self._for(),
            "lastModified": now(),
        }
        if outputs:
            task["output"] = outputs
        self.c.fhir(f"closure ({reason})", {
            "resourceType": "Provenance", "id": f"SYN-CLOSURE-{self.number}", "contained": [task],
            "target": [{"reference": f"#{task_id}"}], "recorded": now(), "agent": [{"who": actor(by)}],
        }, expect=expect)
        return self

    def call_outcome(self, acknowledged: bool, understood: bool, confidence: int | None,
                     by: tuple[str, str, str] | None = None) -> "Case":
        """The nurse navigator records what the patient said on the phone."""
        if self.c.dry_run:
            print("      call outcome: (dry run)")
            return self
        if not self.find_case_id():
            print("      call outcome: UNEXPECTED - the case was not found")
            self.c.failures.append(f"call outcome for {self.name}: the case was not found")
            return self
        self.c.rest("call outcome", f"/cases/{self.case_id}/call-outcome",
                    {"acknowledged": acknowledged, "understood": understood, "confidence": confidence},
                    by or (MONA if self.clinician[2] == GH else HUDA))
        return self

    def reacts(self, delivery: str = "Deliver", response: str = "Acknowledge", failure: str | None = None) -> "Case":
        self.c.behaviour(self.patient, delivery, response, failure=failure)
        return self

    def pause(self, seconds: float = 8) -> "Case":
        """Lets CareLoop send the message and the patient's reaction come back."""
        if not self.c.dry_run:
            time.sleep(seconds)
        return self


class Skipped:
    """Stands in for a case left out by --only: every step does nothing."""

    def __getattr__(self, _name: str):
        return lambda *args, **kwargs: self


ONLY: set[str] = set()


def start(client: Client, slug: str, name: str, number: int, story: str, **kwargs):
    if ONLY and slug not in ONLY:
        return Skipped()
    print(f"\n  {number}  {name}  [{slug}]  {story}")
    return Case(client, slug, name, number, **kwargs)


# --- Cases meant to miss a deadline (run under the demo pathway) ------------------------------------
def seed_overdue(c: Client) -> None:
    print("\nPhase: overdue and escalated")
    start(c, "silent-1", "Amal Rahman", 1001, "notified, never answers: unacknowledged, escalates") \
        .reacts(response="None").result().review()
    start(c, "silent-2", "Noor Haddad", 1002, "notified by text and email, never answers", clinician=SARA, email=True) \
        .reacts(response="None").result().review(urgency="PLACEHOLDER-URGENCY-B")
    start(c, "unreachable", "Mariam Saleh", 1003, "every message fails: unreachable") \
        .reacts(delivery="Fail", response="None", failure="SIMULATED - number not in service").result().review()
    start(c, "handoff-1", "Fatima Khan", 1004, "handoff proposed to Dr. Layla, never accepted: overdue, escalates") \
        .reacts().result().review().pause().consultation()
    start(c, "handoff-2", "Hessa Mansoor", 1005, "handoff proposed, never accepted", clinician=SARA) \
        .reacts().result().review().pause().consultation()
    start(c, "call-waiting", "Salma Yousef", 1006, "asks for a call, nobody calls: call-back overdue") \
        .reacts(response="CallMe").result().review()
    start(c, "unassigned", "Reem Abdullah", 1007, "result with no named owner and no review: facility lead's queue", interpreter=False) \
        .result()
    # At the general hospital, on Dr. Layla's own list.
    start(c, "gh-silent", "Sawsan Idris", 1008, "general hospital: Dr. Layla's case, the patient never answers", clinician=YUSUF) \
        .reacts(response="None").result().review().pause().consultation().consultation_decision("accepted")
    start(c, "gh-missed", "Hala Nasser", 1009, "general hospital: Dr. Layla's case, appointment missed and not rebooked", clinician=YUSUF) \
        .reacts().result().review().pause().consultation().consultation_decision("accepted") \
        .appointment("booked", 1, in_days(-0.01)).appointment("noshow", 2, in_days(-0.01))


# --- Cases that are closed, or in progress and on time (run under the real pathway) -----------------
def seed_ontime(c: Client) -> None:
    print("\nPhase: closed and on time")
    tomorrow, earlier_today = in_days(1), in_days(-0.01)

    start(c, "closed-treated", "Dana Farouk", 1011, "full journey, closed as treated on evidence") \
        .reacts().result().review().pause().call_outcome(True, True, 4).referral().referral_decision("accepted") \
        .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today) \
        .outcome("diagnosis").closure("treated")
    start(c, "closed-surveillance", "Aisha Karim", 1012, "closed on surveillance with a next date", clinician=SARA) \
        .reacts().result().review().pause().call_outcome(True, True, 5).referral().referral_decision("accepted") \
        .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today) \
        .outcome("surveillance-started").closure("on-surveillance", next_surveillance=in_days(180)[:10])
    start(c, "closed-negative", "Shamma Obaid", 1013, "confirmation was negative: closed on evidence", email=True) \
        .reacts().result().review(urgency="PLACEHOLDER-URGENCY-B").pause().call_outcome(True, True, 4).referral().referral_decision("accepted") \
        .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today) \
        .outcome("diagnosis").closure("negative-on-confirmation")

    start(c, "awaiting-referral", "Maha Suleiman", 1014, "acknowledged; referral sent, awaiting Dr. Layla") \
        .reacts().result().review().pause().referral()
    start(c, "booked", "Rania Hamdan", 1015, "referral accepted, appointment booked for tomorrow", clinician=SARA) \
        .reacts().result().review().pause().referral().referral_decision("accepted").appointment("booked", 1, tomorrow)
    start(c, "called", "Iman Darwish", 1016, "asked for a call; Huda called and recorded teach-back") \
        .reacts(response="CallMe").result().review().pause(10).call_outcome(True, True, 4)

    start(c, "referral-rejected", "Jana Qasim", 1017, "referral rejected: back with the owner, new deadline") \
        .reacts().result().review().pause().referral() \
        .referral_decision("rejected", reason="SIMULATED - service not offered at this facility")
    start(c, "missed-appointment", "Nadia Tariq", 1018, "did not attend: appointment to rebook", clinician=SARA) \
        .reacts().result().review().pause().referral().referral_decision("accepted") \
        .appointment("booked", 1, earlier_today).appointment("noshow", 2, earlier_today)
    start(c, "closure-refused", "Huda Mahmoud", 1019, "closure attempted without evidence: refused and audited") \
        .reacts().result().review().pause().closure("treated", by=REKHA, evidence=False, expect=(422,))


    # At the general hospital, so Dr. Layla has cases of her own: each reaches her by a handoff she accepted.
    start(c, "gh-accepted", "Lina Aziz", 1021, "general hospital: handoff accepted by Dr. Layla, who now owns it", clinician=YUSUF) \
        .reacts().result().review().pause().call_outcome(True, True, 5).consultation().consultation_decision("accepted")
    start(c, "gh-treatment", "Farah Nabil", 1022, "general hospital: Dr. Layla's case, treatment plan recorded, treatment to start", clinician=YUSUF) \
        .reacts().result().review().pause().call_outcome(True, True, 4).consultation().consultation_decision("accepted") \
        .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today).outcome("treatment-plan")
    start(c, "gh-booked", "Amira Sabry", 1024, "general hospital: Dr. Layla's case, appointment booked for tomorrow", clinician=YUSUF) \
        .reacts().result().review().pause().call_outcome(True, True, 5).consultation().consultation_decision("accepted").appointment("booked", 1, tomorrow)
    start(c, "gh-closed", "Wafa Jaber", 1023, "general hospital: Dr. Layla's case, closed as treated", clinician=YUSUF, email=True) \
        .reacts().result().review().pause().call_outcome(True, True, 4).consultation().consultation_decision("accepted") \
        .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today).outcome("diagnosis").closure("treated")


# --- Journeys that met every deadline and closed (run under the demo pathway) -----------------------
GIVEN = ["Layan", "Hind", "Mona", "Sana", "Dalal", "Ghada", "Hanan", "Lubna", "Najla", "Rasha", "Samar", "Tala",
         "Widad", "Yasmin", "Zainab", "Abeer", "Bushra", "Duha"]
FAMILY = ["Al Marri", "Bakr", "Chaudhry", "Dsouza", "Elhassan", "Fernandes", "Ghanem", "Hashmi", "Ibrahim", "Jaffer",
          "Kazmi", "Lootah"]


def journey_name(i: int) -> str:
    """A different invented name for each journey."""
    return f"{GIVEN[i % len(GIVEN)]} {FAMILY[(i * 5 + i // len(GIVEN)) % len(FAMILY)]}"


def record_call(case: Case, i: int) -> None:
    """Most patients get a call from a nurse navigator, who records teach-back and a confidence rating.
    A few are not called, and a few cannot yet say the next step back."""
    if i % 9 == 8:
        return
    if i % 11 == 5:
        case.call_outcome(True, False, 2)
    else:
        case.call_outcome(True, True, (4, 5, 5, 3, 4)[i % 5])


def seed_completed(c: Client, count: int = 46) -> None:
    """Varied on purpose: three closure reasons, both clinicians, some reached by email as well,
    most with a nurse's call recorded, some with a treatment plan and then treatment started."""
    general = max(round(count * 13 / 46), 1)
    print(f"\nPhase: {count + general} journeys completed on time")
    earlier_today = in_days(-0.01)
    for i in range(count):
        number = 1101 + i
        kind = ("treated", "negative", "surveillance", "treated", "negative", "treated-after-plan")[i % 6]
        case = start(c, f"done-{number}", journey_name(i), number, f"completed on time, closed: {kind}",
                     clinician=SARA if i % 3 == 1 else REKHA, email=i % 4 == 0)
        case.reacts().result().review(urgency="PLACEHOLDER-URGENCY-B" if i % 5 == 0 else "PLACEHOLDER-URGENCY-A").pause(6)
        record_call(case, i)
        case.referral().referral_decision("accepted") \
            .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today)
        if kind == "treated":
            case.outcome("diagnosis").closure("treated")
        elif kind == "negative":
            case.outcome("diagnosis").closure("negative-on-confirmation")
        elif kind == "surveillance":
            case.outcome("surveillance-started").closure("on-surveillance", next_surveillance=in_days(180)[:10])
        else:
            case.outcome("treatment-plan", part="-PLAN").outcome("treatment-started", part="-START") \
                .closure("treated", part="-START")

    # The same at the general hospital, where each case reaches Dr. Layla by a handoff she accepts.
    for i in range(general):
        number = 1201 + i
        kind = ("treated", "surveillance", "negative", "treated-after-plan")[i % 4]
        case = start(c, f"gh-done-{number}", journey_name(count + i), number, f"general hospital, completed on time, closed: {kind}",
                     clinician=YUSUF, email=i % 3 == 0)
        case.reacts().result().review().pause(6)
        record_call(case, i + 3)
        case.consultation().consultation_decision("accepted") \
            .appointment("booked", 1, earlier_today).appointment("fulfilled", 2, earlier_today)
        if kind == "surveillance":
            case.outcome("surveillance-started").closure("on-surveillance", next_surveillance=in_days(180)[:10])
        elif kind == "treated-after-plan":
            case.outcome("treatment-plan", part="-PLAN").outcome("treatment-started", part="-START") \
                .closure("treated", part="-START")
        else:
            case.outcome("diagnosis").closure("treated" if kind == "treated" else "negative-on-confirmation")


# --- The nurse navigator's calls to patients who are still in progress -----------------------------
def seed_calls(c: Client) -> None:
    print("\nPhase: nurse navigator calls")
    for number, name in [(1014, "Maha Suleiman"), (1015, "Rania Hamdan"), (1017, "Jana Qasim"), (1018, "Nadia Tariq"),
                         (1019, "Huda Mahmoud"), (1004, "Fatima Khan")]:
        print(f"\n  {number}  {name}  Huda called; teach-back recorded")
        case = Case(c, f"call-{number}", name, number)
        case.call_outcome(True, True, (4, 5, 3)[number % 3])


# --- Staff acknowledge escalations (run once the overdue cases have escalated) ----------------------
def seed_acknowledgements(c: Client) -> None:
    """Only a case's current escalation can be acknowledged, by the role it was raised to."""
    print("\nPhase: escalation acknowledgements")
    for patient, story in [("SYN-PT-1001", "the silent patient (Amal Rahman)"), ("SYN-PT-1004", "the stalled handoff (Fatima Khan)")]:
        if c.dry_run:
            print(f"\n  {story}\n      acknowledgement: (dry run)")
            continue
        cases = c.get("/cases?take=200", JOY) or []
        case_id = next((x["caseId"] for x in cases if x.get("patientReference") == patient), None)
        detail = c.get(f"/cases/{case_id}", JOY) if case_id else None
        open_ones = [e for e in (detail or {}).get("escalations", []) if not e.get("resolvedAt")]
        current = max(open_ones, key=lambda e: e["level"], default=None)
        if not current:
            print(f"\n  {story}\n      acknowledgement: UNEXPECTED - the case has no open escalation yet")
            c.failures.append(f"{story}: no open escalation")
            continue
        person = JOY if current["escalatedTo"]["role"] == "facility-lead" else OBAID
        print(f"\n  {person[0]} acknowledges the level {current['level']} escalation on {story}")
        if current.get("acknowledgedAt"):
            print("      acknowledgement: already acknowledged")
            continue
        c.rest("acknowledgement", f"/cases/{case_id}/escalations/{current['escalationId']}/acknowledgement", {}, person)


def main() -> int:
    parser = argparse.ArgumentParser(description="Seed a CareLoop environment with demonstration cases.")
    parser.add_argument("--api", required=True, help="CareLoop API address, for example https://api.example")
    parser.add_argument("--messaging", required=True, help="messaging simulator address")
    parser.add_argument("--phase", required=True, choices=["overdue", "completed", "ontime", "all", "calls", "acknowledge"])
    parser.add_argument("--count", type=int, default=46, help="how many completed journeys at the Family Health Centre; the General Hospital gets about a quarter as many")
    parser.add_argument("--only", default="", help="comma-separated case labels to send, for example gh-closed,booked")
    parser.add_argument("--source", default="his-simulator", help="the sending system's key under ExternalSystems:Systems")
    parser.add_argument("--dry-run", action="store_true", help="print what would be sent and send nothing")
    args = parser.parse_args()

    ONLY.update(label.strip() for label in args.only.split(",") if label.strip())
    client = Client(args.api, args.messaging, args.source, args.dry_run)
    if args.phase in ("overdue", "all"):
        seed_overdue(client)
    if args.phase in ("completed", "all"):
        seed_completed(client, args.count)
    if args.phase in ("ontime", "all"):
        seed_ontime(client)
    if args.phase == "calls":
        seed_calls(client)
    if args.phase == "acknowledge":
        seed_acknowledgements(client)

    print()
    if client.failures:
        print(f"{len(client.failures)} unexpected response(s):")
        for failure in client.failures:
            print("  " + failure)
        return 1
    print("Every event got the response it expected.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
