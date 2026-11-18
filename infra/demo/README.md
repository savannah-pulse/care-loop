# Demonstration data

`seed_demo.py` fills a CareLoop environment with cases for the people on the dashboard's "Signed in as" list, so
that each of them has something to look at. Every patient, name, identifier and address in it is invented.

## Why not the scenarios

The shared scenarios in `src/simulators/scenarios` send their cases to facility `SYN-FAC-A`, which nobody on the
sign-in list belongs to, and all use one patient. The dashboard shows a person the cases at their own facility, so
those cases appear on no one's worklist (`docs/workstreams.md` lists this under WS7 and WS10).

The script plays the hospital system instead: it posts FHIR to CareLoop's `/fhir` boundary, as the HIS simulator
does, but for `family-health-centre` and `general-hospital` and for the staff the dashboard knows. Patients are
reached through the messaging simulator, which the script first tells how each patient reacts.

## What it creates

About eighty cases: about sixty at the Family Health Centre and twenty at the General Hospital. Most are journeys
that met every deadline and closed on evidence, which is what a working programme mostly looks like; the rest are
the cases CareLoop exists to catch.

| Phase | Cases | What they show |
| --- | --- | --- |
| `overdue` | 9 | Patients who never answer or cannot be reached, a handoff nobody accepts, a call-back nobody makes, a result with no owner, a missed appointment not rebooked. Each goes overdue and escalates to the facility lead, then the coordinator |
| `completed` | 59 | Full journeys, each step done by its deadline, closed as treated, on surveillance with a next date, or negative on confirmation. Most include a nurse navigator's call with teach-back and a confidence rating; some record a treatment plan and then treatment started |
| `ontime` | 13 | In progress and on time: awaiting a referral response, appointment booked, call recorded, referral rejected with a new next action, appointment to rebook, a closure refused for lack of evidence, treatment to start; and four closed |
| `calls` | (6 calls) | The nurse navigator calls six patients whose cases are still open |
| `acknowledge` | (2) | Two escalations acknowledged by the role they sit with |

Who gets what on the dashboard's sign-in list:

- **Dr. Rekha, Dr. Sara** (clinicians, Family Health Centre): their own open cases, on time and overdue.
- **Huda** (nurse navigator): the patient waiting for a call back; the calls she recorded show on the cases.
- **Joy** (facility lead): the escalated cases and the result with no owner.
- **Dr. Layla** (receiving clinician, General Hospital): cases she owns after accepting a handoff, including two overdue.
- **Dr. Obaid** (programme coordinator): every case escalated to programme level, without names.

## Running it

CareLoop measures deadlines in real time, and counts a step as "done by the deadline" only once that deadline has
passed. So the data is seeded in two parts, around a change of pathway timing.

1. **With the demo pathway** (deadlines in seconds), seed the cases meant to miss a deadline and the journeys that
   met every one:

   ```bash
   python3 infra/demo/seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase overdue
   ```

   ```bash
   python3 infra/demo/seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase completed
   ```

   The second takes about fifteen minutes. Then wait two minutes: the overdue cases escalate to the facility lead
   and, 48 seconds later, to the coordinator, and the completed journeys' deadlines pass and count as met.

2. **Switch the environment to the real pathway** (deadlines in hours and days), so that what is seeded next stays
   on time. On Azure, redeploy with `DEMO_TIMING=false` (see `infra/azure/README.md`). A redeployment restarts the
   simulators, which empties their inboxes; to keep the messages from step 1, remove the `Pathways__Directory` and
   `DeadlineWorker__Interval` settings from the `api` container app instead, and set `DEMO_TIMING=false` on every
   later deployment so the change is not undone.

3. **Seed the in-progress cases**, then the calls and the acknowledgements:

   ```bash
   python3 infra/demo/seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase ontime
   ```

   ```bash
   python3 infra/demo/seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase calls
   ```

   ```bash
   python3 infra/demo/seed_demo.py --api https://<api> --messaging https://<messaging simulator> --phase acknowledge
   ```

The script prints the response to every event and ends with "Every event got the response it expected", or lists
the ones that did not. It is safe to run twice: every resource has a fixed identifier and version, so CareLoop
treats a repeat as a duplicate. `--only <label>,<label>` sends just the named cases, `--count` changes how many
completed journeys there are, and `--dry-run` sends nothing.

For a local Docker Compose stack, where timing does not matter for a quick look, `--phase all` does the three case
phases at once.

## Things to know

- **The impact page's times are short.** "Time to each step" is real clock time, and these journeys ran in seconds.
  The rates (done by the deadline, understood, closed) are what the data is shaped for.
- **On-time cases do not stay on time for ever.** Under the real pathway most deadlines are one day; the referral
  response is four hours and a call-back two. Seed the on-time phase within a day of the demonstration.
- **Starting again** means emptying the database: stop the API, drop and recreate the `careloop` database, start
  the API (it applies its migrations) and restart both simulators.
- **The simulators forget on restart.** Their inboxes and logs are in memory. The cases in CareLoop are not
  affected, but the messages sent for them are no longer there to look at.
- **Live scenario runs are slow under the real pathway.** The simulators still pace a scenario at two seconds per
  hour, but CareLoop's deadlines now last real hours, so a scenario started from a simulator's screen does not
  escalate within seconds.
- The names read like real people because a demonstration needs them to. They are invented, and the identifiers
  beside them are all `SYN-`.
