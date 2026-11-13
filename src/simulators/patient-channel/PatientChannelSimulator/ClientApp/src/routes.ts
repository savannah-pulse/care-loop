/**
 * Client-side route paths, one per simulator screen.
 *
 * @remarks
 * None of these may start with a prefix served by the .NET host (`/api`, `/fhir`,
 * `/scenarios`, `/health`), otherwise a page refresh would hit the API instead of the UI.
 */
export const ROUTES = {
  /** Patient inbox (the landing screen). */
  inbox: '/',
  /** Patient case-status view. */
  caseStatus: '/case-status',
  /** Per-patient behaviour controls. */
  patientBehaviours: '/patient-behaviours',
  /** Scenario runner. */
  scenarioRunner: '/runner',
  /** FHIR message log. */
  messageLog: '/message-log',
} as const;
