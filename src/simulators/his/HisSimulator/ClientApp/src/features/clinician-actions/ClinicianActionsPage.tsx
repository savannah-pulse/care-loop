import { PlaceholderPanel } from '../../components';

/** Props for {@link ClinicianActionsPage}. */
export interface ClinicianActionsPageProps {
  /**
   * Base URL of the HIS simulator API. Omit it to call the same origin that serves the ClientApp
   * (the Vite dev server proxies those calls to the simulator).
   */
  apiBaseUrl?: string;
}

/** Translation keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'clinicianActions.planned.cases',
  'clinicianActions.planned.review',
  'clinicianActions.planned.referral',
  'clinicianActions.planned.outcome',
  'clinicianActions.planned.closure',
] as const;

/**
 * The "act as clinician" screen: manual actions a person would take in the hospital system.
 *
 * @remarks
 * Urgency and closure are human decisions. This screen lets a person make them by hand; the simulator only forwards the decision to CareLoop as FHIR. Urgency and closure-reason choices must be placeholder codes supplied by configuration, never invented in the UI. A closure that CareLoop refuses is shown as refused.
 */
export function ClinicianActionsPage(_props: ClinicianActionsPageProps) {
  // TODO(WS-7): load GET /cases and post to /actions/clinical-review, /actions/referral-decision, /actions/outcome and /actions/closure.
  return (
    <PlaceholderPanel
      titleKey="clinicianActions.title"
      descriptionKey="clinicianActions.description"
      plannedKeys={PLANNED_KEYS}
    />
  );
}
