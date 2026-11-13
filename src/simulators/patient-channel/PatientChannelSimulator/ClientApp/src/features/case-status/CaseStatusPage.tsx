import { useTranslation } from 'react-i18next';

import { PlaceholderNotice } from '../../components';

/** i18n keys of the behaviours planned for this screen. */
const PLANNED_KEYS = ['caseStatus.planned.lookup', 'caseStatus.planned.status'] as const;

/** Props for {@link CaseStatusPage}. */
export interface CaseStatusPageProps {
  /** Logical id of the case `Task` to show. When omitted the screen asks for one. */
  taskId?: string;
}

/**
 * The patient's view of a case, read from the CareLoop case `Task`.
 *
 * @remarks
 * Shows coordination status only (stage, deadline, last update). It never shows a result,
 * diagnosis or urgency: the patient channel is not a clinical system. Placeholder until WS8.
 */
export function CaseStatusPage(_props: CaseStatusPageProps) {
  const { t } = useTranslation();

  // TODO(WS-8): add a Task id lookup and render the PatientCaseStatus returned by getCaseStatus().
  return (
    <section aria-labelledby="lookup-title">
      <h2 id="lookup-title">{t('caseStatus.title')}</h2>
      <p>{t('caseStatus.description')}</p>
      <PlaceholderNotice plannedKeys={PLANNED_KEYS} />
    </section>
  );
}
