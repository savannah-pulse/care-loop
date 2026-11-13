import { useTranslation } from 'react-i18next';

import type { PatientBehaviourAssignment } from '../../api';
import { PlaceholderNotice } from '../../components';

/** i18n keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'patientBehaviours.planned.list',
  'patientBehaviours.planned.delivery',
  'patientBehaviours.planned.response',
  'patientBehaviours.planned.timing',
  'patientBehaviours.planned.default',
] as const;

/** Props for {@link PatientBehavioursPage}. */
export interface PatientBehavioursPageProps {
  /** Called after a patient's behaviour has been saved, with the stored assignment. */
  onChanged?: (assignment: PatientBehaviourAssignment) => void;
}

/**
 * Shows and edits how each simulated patient reacts when CareLoop notifies them.
 *
 * @remarks
 * A behaviour has two halves: what the channel reports (delivered, failed, or nothing) and what
 * the patient then does (nothing, acknowledge, confirm understanding, or ask for a call). It is
 * a reaction, applied only when a notification actually arrives, so a patient is never shown as
 * acknowledging a message CareLoop did not send. Delivery alone is never an acknowledgement.
 * Scenario runs set these behaviours; this screen is for setting them by hand. Placeholder until WS8.
 */
export function PatientBehavioursPage(_props: PatientBehavioursPageProps) {
  const { t } = useTranslation();

  // TODO(WS-8): list assignments with listPatientBehaviours(), show the default from getDefaultPatientBehaviour(), and save with setPatientBehaviour(), setDefaultPatientBehaviour() and removePatientBehaviour().
  return (
    <section aria-labelledby="behaviours-title">
      <h2 id="behaviours-title">{t('patientBehaviours.title')}</h2>
      <p>{t('patientBehaviours.description')}</p>
      <PlaceholderNotice plannedKeys={PLANNED_KEYS} />
    </section>
  );
}
