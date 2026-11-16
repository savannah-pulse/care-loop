/**
 * Client-side route paths, one per simulator screen.
 *
 * @remarks
 * None of these may start with a prefix served by the .NET host (`/api`, `/messages`,
 * `/scenarios`, `/health`), otherwise a page refresh would hit the API instead of the UI.
 */
export const ROUTES = {
  /** The inboxes (the landing screen). */
  inbox: '/',
  /** Per-patient behaviour controls. */
  patientBehaviours: '/patient-behaviours',
  /** Scenario runner. */
  scenarioRunner: '/runner',
  /** Message log. */
  messageLog: '/message-log',
} as const;
