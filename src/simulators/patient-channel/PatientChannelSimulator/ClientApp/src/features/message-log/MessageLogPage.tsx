import { useTranslation } from 'react-i18next';

import { PlaceholderNotice } from '../../components';

/** i18n keys of the behaviours planned for this screen. */
const PLANNED_KEYS = ['messageLog.planned.list', 'messageLog.planned.detail'] as const;

/** Props for {@link MessageLogPage}. */
export interface MessageLogPageProps {
  /** Maximum number of log entries to show. All entries are shown when omitted. */
  maxEntries?: number;
}

/**
 * The log of every FHIR exchange between this simulator and CareLoop.
 *
 * @remarks
 * Exists so a demo audience can see exactly what crossed the FHIR boundary, including that
 * SMS-safe content carries no sensitive detail. Placeholder until WS8.
 */
export function MessageLogPage(_props: MessageLogPageProps) {
  const { t } = useTranslation();

  // TODO(WS-8): load entries with listFhirLog() and render a list with a FHIR JSON detail view.
  return (
    <section aria-labelledby="list-title">
      <h2 id="list-title">{t('messageLog.title')}</h2>
      <p>{t('messageLog.description')}</p>
      <PlaceholderNotice plannedKeys={PLANNED_KEYS} />
    </section>
  );
}
