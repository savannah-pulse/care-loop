import { useId, useState, type FormEvent } from 'react'
import { recordCallOutcome, type CaseDetail, type FailureKind } from '../../data'
import { t, type MessageKey } from '../../i18n'
import {
  CONFIDENCE_RATINGS,
  EMPTY_CALL_OUTCOME,
  canRecord,
  recordedConfidence,
  toCallOutcome,
  type CallOutcomeDraft,
  type CallOutcomeOffer,
} from './callOutcome'

/** Props for {@link CallOutcomeCard}. */
export interface CallOutcomeCardProps {
  /** The case the call was about. */
  readonly detail: CaseDetail
  /** `open` to record, `notYet` when no message has been sent to the patient. */
  readonly offer: Exclude<CallOutcomeOffer, 'hidden'>
  /** The reason given for a case outside the person's team, when there is one. */
  readonly reason?: string
  /** Called with the case as it now stands, after the API recorded the outcome. */
  readonly onRecorded: (detail: CaseDetail) => void
}

const FAILURES: Partial<Record<FailureKind, MessageKey>> = {
  forbidden: 'callOutcome.failed.forbidden',
  conflict: 'callOutcome.failed.conflict',
  invalid: 'callOutcome.failed.invalid',
}

/**
 * "After a call with the patient": where a nurse navigator records what the patient said on the
 * phone.
 *
 * @remarks
 * Patients are reached by text message and email only. A reply there can say "I will come" or ask
 * for a call, and nothing more, so whether the patient understood the next step and how confident
 * they feel are recorded here, by a person, after a call. The three are kept apart: saying the
 * next step back is not the same as agreeing to come, and a rating is a measure, never an
 * acknowledgement. Posts `POST /cases/{caseId}/call-outcome`; recording never changes who is
 * responsible for the case. The API allows nurse navigators only and the screen shows the card to
 * them only. Nothing is ticked for the nurse, and the button stays off until something is.
 */
export function CallOutcomeCard({ detail, offer, reason, onRecorded }: CallOutcomeCardProps) {
  const headingId = useId()
  const ratingName = useId()
  const [draft, setDraft] = useState<CallOutcomeDraft>(EMPTY_CALL_OUTCOME)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<FailureKind>()
  const current = recordedConfidence(detail)
  const locked = busy || offer !== 'open'

  const submit = async (event: FormEvent): Promise<void> => {
    event.preventDefault()
    if (!canRecord(draft) || locked) {
      return
    }
    setBusy(true)
    setFailure(undefined)
    const outcome = await recordCallOutcome(detail.caseId, toCallOutcome(draft), reason)
    setBusy(false)
    if (outcome.ok) {
      setDraft(EMPTY_CALL_OUTCOME)
      onRecorded(outcome.data)
      return
    }
    setFailure(outcome.failure.kind)
  }

  return (
    <section className="card call-card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('callOutcome.title')}
      </h2>
      <p className="sub-line">{t('callOutcome.lede')}</p>
      {current === undefined ? null : (
        <p className="sub-line call-current">{t('callOutcome.currentConfidence', { rating: current })}</p>
      )}
      <form className="call-form" onSubmit={(event) => void submit(event)}>
        <label className="call-check">
          <input
            type="checkbox"
            checked={draft.willCome}
            disabled={locked}
            onChange={(event) => setDraft({ ...draft, willCome: event.target.checked })}
          />
          <span>{t('callOutcome.willCome')}</span>
        </label>
        <label className="call-check">
          <input
            type="checkbox"
            checked={draft.saidBack}
            disabled={locked}
            onChange={(event) => setDraft({ ...draft, saidBack: event.target.checked })}
          />
          <span>{t('callOutcome.saidBack')}</span>
        </label>
        <fieldset className="call-rating" disabled={locked}>
          <legend>{t('callOutcome.confidence')}</legend>
          <div className="call-rating-options">
            <label>
              <input
                type="radio"
                name={ratingName}
                checked={draft.confidence === undefined}
                onChange={() => setDraft({ ...draft, confidence: undefined })}
              />
              <span>{t('callOutcome.notAsked')}</span>
            </label>
            {CONFIDENCE_RATINGS.map((rating) => (
              <label key={rating}>
                <input
                  type="radio"
                  name={ratingName}
                  checked={draft.confidence === rating}
                  aria-label={t('callOutcome.rating', { rating })}
                  onChange={() => setDraft({ ...draft, confidence: rating })}
                />
                <span aria-hidden="true">{rating}</span>
              </label>
            ))}
          </div>
          <p className="sub-line">{t('callOutcome.scale')}</p>
        </fieldset>
        {offer === 'notYet' ? <p className="sub-line">{t('callOutcome.notYet')}</p> : null}
        {failure ? (
          <p className="field-error" role="alert">
            {t(FAILURES[failure] ?? 'callOutcome.failed.error')}
          </p>
        ) : null}
        <button type="submit" className="btn primary" disabled={locked || !canRecord(draft)}>
          {busy ? t('callOutcome.recording') : t('callOutcome.record')}
        </button>
      </form>
      <p className="sub-line">{t('callOutcome.note')}</p>
    </section>
  )
}
