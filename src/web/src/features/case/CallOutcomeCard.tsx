import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { recordCallOutcome, type CaseDetail, type FailureKind } from '../../data'
import { formatTime } from '../../format'
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
  /**
   * Called when the API refused part of the call (409): the case changed meanwhile. Some outcomes may already be
   * saved, since each is its own event, so the page reads the case and the timeline again.
   */
  readonly onConflict?: () => void
  /** The moment to stamp a recorded call with; now by default. */
  readonly now?: () => Date
}

const FAILURES: Partial<Record<FailureKind, MessageKey>> = {
  forbidden: 'callOutcome.failed.forbidden',
  conflict: 'callOutcome.failed.conflict',
  invalid: 'callOutcome.failed.invalid',
}

/** What was recorded from the last call on this card, and when. */
interface LastCall {
  readonly at: Date
  readonly draft: CallOutcomeDraft
}

/**
 * Lists what a recorded call said, in plain words.
 *
 * @param draft - What was recorded.
 * @returns For example `will come, said the next step back, 4 out of 5`.
 */
function describeCall(draft: CallOutcomeDraft): string {
  const parts: string[] = []
  if (draft.willCome) {
    parts.push(t('callOutcome.summary.willCome'))
  }
  if (draft.saidBack) {
    parts.push(t('callOutcome.summary.saidBack'))
  }
  if (draft.confidence !== undefined) {
    parts.push(t('callOutcome.rating', { rating: draft.confidence }))
  }
  return parts.join(t('callOutcome.summary.separator'))
}

/**
 * "After a call with the patient": where a nurse navigator records what the patient said on the
 * phone.
 *
 * @remarks
 * Patients are reached by text message and email only. A reply there can say "I got this message"
 * or ask for a call, and nothing more, so whether the patient will come, whether they understood the
 * next step and how confident they feel are recorded here, by a person, after a call. The three are
 * kept apart: saying the next step back is not the same as agreeing to come, and a rating is a
 * measure, never an acknowledgement. Posts `POST /cases/{caseId}/call-outcome`; recording never
 * changes who is responsible for the case. The API allows nurse navigators only and the screen shows
 * the card to them only. Nothing is ticked for the nurse.
 *
 * Keyboard and screen-reader users never lose their place: the button stays focusable while saving
 * and while nothing is ticked (`aria-disabled`, and a press then does nothing), focus moves to the
 * heading after a success, where the card says what was just recorded, and to the message after a
 * refusal. The hints are tied to their controls, and when nothing can be recorded yet the card says
 * why before the controls.
 */
export function CallOutcomeCard({ detail, offer, reason, onRecorded, onConflict, now = () => new Date() }: CallOutcomeCardProps) {
  const headingId = useId()
  const ratingName = useId()
  const scaleId = useId()
  const notYetId = useId()
  const headingRef = useRef<HTMLHeadingElement>(null)
  const alertRef = useRef<HTMLParagraphElement>(null)
  const [draft, setDraft] = useState<CallOutcomeDraft>(EMPTY_CALL_OUTCOME)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<FailureKind>()
  const [last, setLast] = useState<LastCall>()
  const current = recordedConfidence(detail)
  const locked = offer !== 'open'
  const sendable = !busy && !locked && canRecord(draft)

  useEffect(() => {
    if (failure) {
      alertRef.current?.focus()
    }
  }, [failure])

  const submit = async (event: FormEvent): Promise<void> => {
    event.preventDefault()
    if (!sendable) {
      return
    }
    setBusy(true)
    setFailure(undefined)
    const sent = draft
    const outcome = await recordCallOutcome(detail.caseId, toCallOutcome(sent), reason)
    setBusy(false)
    if (outcome.ok) {
      setDraft(EMPTY_CALL_OUTCOME)
      setLast({ at: now(), draft: sent })
      onRecorded(outcome.data)
      headingRef.current?.focus()
      return
    }
    setFailure(outcome.failure.kind)
    if (outcome.failure.kind === 'conflict') {
      onConflict?.()
    }
  }

  return (
    <section className="card call-card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId} ref={headingRef} tabIndex={-1}>
        {t('callOutcome.title')}
      </h2>
      <p className="sub-line">{t('callOutcome.lede')}</p>
      {locked ? (
        <p className="sub-line call-not-yet" id={notYetId}>
          {t('callOutcome.notYet')}
        </p>
      ) : null}
      {last ? (
        <p className="sub-line call-current" role="status">
          {t('callOutcome.lastCall', { time: formatTime(last.at), outcome: describeCall(last.draft) })}
        </p>
      ) : current === undefined ? null : (
        <p className="sub-line call-current">{t('callOutcome.currentConfidence', { rating: current })}</p>
      )}
      <form
        className="call-form"
        aria-describedby={locked ? notYetId : undefined}
        onSubmit={(event) => void submit(event)}
      >
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
        <fieldset className="call-rating" disabled={locked} aria-describedby={scaleId}>
          <legend>{t('callOutcome.confidence')}</legend>
          <label className="call-not-asked">
            <input
              type="radio"
              name={ratingName}
              checked={draft.confidence === undefined}
              onChange={() => setDraft({ ...draft, confidence: undefined })}
            />
            <span>{t('callOutcome.notAsked')}</span>
          </label>
          <div className="call-rating-options">
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
          <p className="sub-line" id={scaleId}>
            {t('callOutcome.scale')}
          </p>
        </fieldset>
        {failure ? (
          <p className="field-error" role="alert" ref={alertRef} tabIndex={-1}>
            {t(FAILURES[failure] ?? 'callOutcome.failed.error')}
          </p>
        ) : null}
        <button type="submit" className="btn primary" aria-disabled={!sendable}>
          {busy ? t('callOutcome.recording') : t('callOutcome.record')}
        </button>
      </form>
      <p className="sub-line">{t('callOutcome.note')}</p>
    </section>
  )
}
