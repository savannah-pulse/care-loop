import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  getDefaultPatientBehaviour,
  listPatientBehaviours,
  removePatientBehaviour,
  setDefaultPatientBehaviour,
  setPatientBehaviour,
} from '../../api';
import type { PatientBehaviour, PatientBehaviourAssignment } from '../../api';
import { SimulatedBadge } from '../../components';
import { errorMessage, useLoad } from '../../hooks';

import { BehaviourForm, DELIVER_AND_DO_NOTHING } from './BehaviourForm';
import { fromDotNetDuration } from './duration';

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
 * the patient then does (nothing, acknowledge, confirm understanding, ask for a call, or rate
 * their confidence). It is a reaction, applied only when a notification actually arrives, so a
 * patient is never shown as acknowledging a message CareLoop did not send. Delivery alone is
 * never an acknowledgement. Scenario runs set these behaviours; this screen is for setting them
 * by hand, and for the default that every other patient gets. A change applies to notifications
 * received from then on.
 */
export function PatientBehavioursPage({ onChanged }: PatientBehavioursPageProps) {
  const { t } = useTranslation();
  const assignments = useLoad(listPatientBehaviours);
  const fallback = useLoad(getDefaultPatientBehaviour);
  const [removeError, setRemoveError] = useState<string | null>(null);

  const describeResponse = (behaviour: PatientBehaviour) => {
    const response = t(`patientBehaviours.response.${behaviour.response}`);
    return behaviour.confidence !== null && behaviour.response !== 'RateConfidence'
      ? t('patientBehaviours.responseWithRating', { response, rating: behaviour.confidence })
      : response;
  };

  const saveDefault = async (behaviour: PatientBehaviour) => {
    const stored = await setDefaultPatientBehaviour(behaviour);
    fallback.setData(() => stored);
  };

  const saveForPatient = async (behaviour: PatientBehaviour, patientSyntheticId: string) => {
    const stored = await setPatientBehaviour(patientSyntheticId, behaviour);
    assignments.reload();
    onChanged?.(stored);
  };

  const remove = async (patientSyntheticId: string) => {
    setRemoveError(null);
    try {
      await removePatientBehaviour(patientSyntheticId);
      assignments.reload();
    } catch (failure) {
      setRemoveError(errorMessage(failure));
    }
  };

  const error = assignments.error ?? fallback.error ?? removeError;

  return (
    <section aria-labelledby="behaviours-title">
      <h2 id="behaviours-title">{t('patientBehaviours.title')}</h2>
      <p>{t('patientBehaviours.description')}</p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}

      <article className="panel" aria-labelledby="default-behaviour-title">
        <h3 id="default-behaviour-title">{t('patientBehaviours.defaultHeading')}</h3>
        <p>{t('patientBehaviours.defaultDescription')}</p>
        {fallback.data ? (
          <BehaviourForm
            idPrefix="default"
            initial={fallback.data}
            submitLabel={t('patientBehaviours.saveDefault')}
            onSave={saveDefault}
          />
        ) : (
          fallback.loading && <p>{t('common.loading')}</p>
        )}
      </article>

      <article className="panel" aria-labelledby="own-behaviour-title">
        <h3 id="own-behaviour-title">{t('patientBehaviours.listHeading')}</h3>
        {assignments.loading && <p>{t('common.loading')}</p>}
        {assignments.data?.length === 0 && <p>{t('patientBehaviours.listEmpty')}</p>}
        {assignments.data && assignments.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th scope="col">{t('patientBehaviours.columns.patient')}</th>
                <th scope="col">{t('patientBehaviours.columns.delivery')}</th>
                <th scope="col">{t('patientBehaviours.columns.response')}</th>
                <th scope="col">{t('patientBehaviours.columns.timing')}</th>
                <th scope="col">{t('patientBehaviours.columns.setBy')}</th>
                <th scope="col">{t('patientBehaviours.columns.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {assignments.data.map(({ patientSyntheticId, behaviour, scenarioName }) => (
                <tr key={patientSyntheticId}>
                  <th scope="row">
                    {patientSyntheticId} <SimulatedBadge />
                  </th>
                  <td>{t(`patientBehaviours.delivery.${behaviour.delivery}`)}</td>
                  <td>{describeResponse(behaviour)}</td>
                  <td>
                    {t('patientBehaviours.timing', {
                      seconds: fromDotNetDuration(behaviour.respondAfter),
                      notification: behaviour.respondToNotification,
                    })}
                  </td>
                  <td>
                    {scenarioName
                      ? t('patientBehaviours.setByScenario', { name: scenarioName })
                      : t('patientBehaviours.setByHand')}
                  </td>
                  <td>
                    <button type="button" onClick={() => void remove(patientSyntheticId)}>
                      {t('patientBehaviours.remove', { patient: patientSyntheticId })}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </article>

      <article className="panel" aria-labelledby="add-behaviour-title">
        <h3 id="add-behaviour-title">{t('patientBehaviours.addHeading')}</h3>
        <BehaviourForm
          idPrefix="patient"
          initial={DELIVER_AND_DO_NOTHING}
          submitLabel={t('patientBehaviours.save')}
          askPatientId
          onSave={saveForPatient}
        />
      </article>
    </section>
  );
}
