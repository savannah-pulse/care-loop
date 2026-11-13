import { useTranslation } from 'react-i18next';

import { PlaceholderNotice } from '../../components';

/** i18n keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'inbox.planned.list',
  'inbox.planned.acknowledge',
  'inbox.planned.understand',
  'inbox.planned.callMe',
] as const;

/** Props for {@link InboxPage}. */
export interface InboxPageProps {
  /** How often to re-read the inbox, in milliseconds. Polling is off when omitted. */
  pollIntervalMs?: number;
}

/**
 * The simulated patient inbox: messages CareLoop sent to the patient, each with acknowledge,
 * "I understand" (teach-back) and "call me" actions.
 *
 * @remarks
 * A message being delivered is not an acknowledgement. Only the patient's explicit action here
 * (or a scripted scenario step) reports a response to CareLoop. Placeholder until WS8.
 */
export function InboxPage(_props: InboxPageProps) {
  const { t } = useTranslation();

  // TODO(WS-8): load messages with listInbox(), render channel, delivery and response state, and wire the three actions to actOnInboxMessage().
  return (
    <section aria-labelledby="list-title">
      <h2 id="list-title">{t('inbox.title')}</h2>
      <p>{t('inbox.description')}</p>
      <PlaceholderNotice plannedKeys={PLANNED_KEYS} />
    </section>
  );
}
