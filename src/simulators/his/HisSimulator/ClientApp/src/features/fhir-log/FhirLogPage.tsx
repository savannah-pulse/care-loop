import { PlaceholderPanel } from '../../components';

/** Props for {@link FhirLogPage}. */
export interface FhirLogPageProps {
  /**
   * Base URL of the HIS simulator API. Omit it to call the same origin that serves the ClientApp
   * (the Vite dev server proxies those calls to the simulator).
   */
  apiBaseUrl?: string;
}

/** Translation keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'fhirLog.planned.sent',
  'fhirLog.planned.received',
  'fhirLog.planned.filter',
  'fhirLog.planned.body',
] as const;

/**
 * The FHIR message log screen: every FHIR message sent to or received from CareLoop.
 *
 * @remarks
 * The log shows the exact wire content, which is how the demo proves that the simulator and CareLoop talk only through FHIR. Every entry is a simulated message.
 */
export function FhirLogPage(_props: FhirLogPageProps) {
  // TODO(WS-7): load GET /fhir-log with direction and caseRef filters and show each entry's FHIR JSON.
  return (
    <PlaceholderPanel
      titleKey="fhirLog.title"
      descriptionKey="fhirLog.description"
      plannedKeys={PLANNED_KEYS}
    />
  );
}
