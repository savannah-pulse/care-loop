import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import type { DeliveryBehaviour, PatientBehaviour, PatientResponseBehaviour } from '../../api';

import { fromDotNetDuration, toDotNetDuration } from './duration';

/** The delivery outcomes, in the order they are offered. */
const DELIVERIES: readonly DeliveryBehaviour[] = ['Deliver', 'Fail', 'StaySilent'];

/** The patient responses, in the order they are offered. */
const RESPONSES: readonly PatientResponseBehaviour[] = ['None', 'Acknowledge', 'CallMe'];

/** The behaviour a new form starts from: delivered, and the patient does nothing. */
export const DELIVER_AND_DO_NOTHING: PatientBehaviour = {
  delivery: 'Deliver',
  response: 'None',
  failureReason: null,
  respondAfter: '00:00:00',
  respondToNotification: 1,
  simulated: true,
};

/** Props for {@link BehaviourForm}. */
export interface BehaviourFormProps {
  /** Prefix that makes the form's field ids unique on the screen. */
  idPrefix: string;
  /** The behaviour the form starts from. */
  initial: PatientBehaviour;
  /** Text of the save button. */
  submitLabel: string;
  /** Whether to ask for the patient identifier as well (for setting one patient's behaviour). */
  askPatientId?: boolean;
  /**
   * Saves the behaviour. The promise rejects when the host refuses it; the form shows the reason.
   * `patientSyntheticId` is the entered identifier, or an empty string when the form does not ask for one.
   */
  onSave: (behaviour: PatientBehaviour, patientSyntheticId: string) => Promise<void>;
}

/**
 * The fields of one patient behaviour: what the channel reports and what the patient then does.
 *
 * @remarks
 * The form enforces the same rules as the host before sending anything: a failed delivery needs
 * a reason, and a patient can only respond (or rate) when the notification is delivered, because
 * delivery is a transport fact and a response needs something to respond to. Choosing a delivery
 * other than "delivered" therefore resets the response to "does nothing".
 */
export function BehaviourForm({
  idPrefix,
  initial,
  submitLabel,
  askPatientId,
  onSave,
}: BehaviourFormProps) {
  const { t } = useTranslation();
  const [patientId, setPatientId] = useState('');
  const [delivery, setDelivery] = useState(initial.delivery);
  const [failureReason, setFailureReason] = useState(initial.failureReason ?? '');
  const [response, setResponse] = useState(initial.response);
  const [respondAfter, setRespondAfter] = useState(
    String(fromDotNetDuration(initial.respondAfter)),
  );
  const [respondTo, setRespondTo] = useState(String(initial.respondToNotification));
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);

  const responds = delivery === 'Deliver';
  const effectiveResponse: PatientResponseBehaviour = responds ? response : 'None';
  const id = (field: string) => `${idPrefix}-${field}`;

  const validate = (): string | null => {
    if (askPatientId && !/^SYN-.+/.test(patientId.trim())) {
      return t('patientBehaviours.validation.patientId');
    }

    if (delivery === 'Fail' && failureReason.trim().length === 0) {
      return t('patientBehaviours.validation.failureReason');
    }

    if (!(Number(respondAfter) >= 0)) {
      return t('patientBehaviours.validation.respondAfter');
    }

    if (!(Number.isInteger(Number(respondTo)) && Number(respondTo) >= 1)) {
      return t('patientBehaviours.validation.respondToNotification');
    }

    return null;
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaved(false);
    const problem = validate();
    setError(problem);
    if (problem) {
      return;
    }

    setBusy(true);
    try {
      await onSave(
        {
          delivery,
          response: effectiveResponse,
          failureReason: delivery === 'Fail' ? failureReason.trim() : null,
          respondAfter: toDotNetDuration(Number(respondAfter)),
          respondToNotification: Number(respondTo),
          simulated: true,
        },
        patientId.trim(),
      );
      setSaved(true);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : String(failure));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="behaviour-form" onSubmit={(event) => void submit(event)} noValidate>
      {askPatientId && (
        <div className="field">
          <label htmlFor={id('patient')}>{t('patientBehaviours.patientIdLabel')}</label>
          <input
            id={id('patient')}
            value={patientId}
            onChange={(event) => setPatientId(event.target.value)}
            aria-describedby={id('patient-hint')}
            autoComplete="off"
          />
          <small id={id('patient-hint')}>{t('patientBehaviours.patientIdHint')}</small>
        </div>
      )}

      <div className="field">
        <label htmlFor={id('delivery')}>{t('patientBehaviours.fields.delivery')}</label>
        <select
          id={id('delivery')}
          value={delivery}
          onChange={(event) => setDelivery(event.target.value as DeliveryBehaviour)}
        >
          {DELIVERIES.map((value) => (
            <option key={value} value={value}>
              {t(`patientBehaviours.delivery.${value}`)}
            </option>
          ))}
        </select>
      </div>

      {delivery === 'Fail' && (
        <div className="field">
          <label htmlFor={id('reason')}>{t('patientBehaviours.fields.failureReason')}</label>
          <input
            id={id('reason')}
            value={failureReason}
            onChange={(event) => setFailureReason(event.target.value)}
          />
        </div>
      )}

      <div className="field">
        <label htmlFor={id('response')}>{t('patientBehaviours.fields.response')}</label>
        <select
          id={id('response')}
          value={effectiveResponse}
          disabled={!responds}
          aria-describedby={id('response-hint')}
          onChange={(event) => setResponse(event.target.value as PatientResponseBehaviour)}
        >
          {RESPONSES.map((value) => (
            <option key={value} value={value}>
              {t(`patientBehaviours.response.${value}`)}
            </option>
          ))}
        </select>
        <small id={id('response-hint')}>{t('patientBehaviours.responseNeedsDelivery')}</small>
      </div>

      <div className="field">
        <label htmlFor={id('after')}>{t('patientBehaviours.fields.respondAfter')}</label>
        <input
          id={id('after')}
          type="number"
          min="0"
          step="any"
          value={respondAfter}
          onChange={(event) => setRespondAfter(event.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor={id('respond-to')}>
          {t('patientBehaviours.fields.respondToNotification')}
        </label>
        <input
          id={id('respond-to')}
          type="number"
          min="1"
          step="1"
          value={respondTo}
          onChange={(event) => setRespondTo(event.target.value)}
        />
      </div>

      <button type="submit" disabled={busy}>
        {submitLabel}
      </button>
      {saved && <span role="status">{t('patientBehaviours.saved')}</span>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </form>
  );
}
