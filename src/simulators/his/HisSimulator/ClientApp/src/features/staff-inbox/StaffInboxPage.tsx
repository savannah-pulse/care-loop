import { PlaceholderPanel } from '../../components';

/** Props for {@link StaffInboxPage}. */
export interface StaffInboxPageProps {
  /**
   * Base URL of the HIS simulator API. Omit it to call the same origin that serves the ClientApp
   * (the Vite dev server proxies those calls to the simulator).
   */
  apiBaseUrl?: string;
}

/** Translation keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'staffInbox.planned.list',
  'staffInbox.planned.filter',
  'staffInbox.planned.detail',
] as const;

/**
 * The staff inbox screen: the simulated messages CareLoop has sent to hospital staff.
 *
 * @remarks
 * Items are escalations, handoff proposals, rejections, call requests, handoff summaries and staff notifications. Staff notifications carry no patient detail. Seeing an item here is not an acknowledgement: nothing is accepted until a person acts on the clinician actions screen.
 */
export function StaffInboxPage(_props: StaffInboxPageProps) {
  // TODO(WS-7): load GET /staff-inbox (optionally ?recipient=), poll for new items and open one via GET /staff-inbox/{id}.
  return (
    <PlaceholderPanel
      titleKey="staffInbox.title"
      descriptionKey="staffInbox.description"
      plannedKeys={PLANNED_KEYS}
    />
  );
}
