import { useEffect, useId, useRef, useState } from 'react'
import { Icon } from '../../components'
import { draftPatientMessage, type AiText, type CaseDetail, type FailureKind, type MessageChannel } from '../../data'
import { formatTime } from '../../format'
import { t, type MessageKey } from '../../i18n'

/** Props for {@link PatientMessageCard}. */
export interface PatientMessageCardProps {
  /** The case the message would be about. */
  readonly detail: CaseDetail
  /** Why the reader opened a case outside their team, if they did; sent with the request. */
  readonly reason?: string
}

const CHANNELS: readonly { readonly value: MessageChannel; readonly label: MessageKey }[] = [
  { value: 'Sms', label: 'patientMessage.channel.sms' },
  { value: 'Email', label: 'patientMessage.channel.email' },
]

const FAILURES: Partial<Record<FailureKind, MessageKey>> = {
  conflict: 'patientMessage.failed.off',
  forbidden: 'patientMessage.failed.forbidden',
}

/**
 * Lets a nurse navigator or clinician ask AI for a draft of a message to the patient, and read it.
 *
 * @remarks
 * A draft only: nothing here sends a message, and nothing is stored. The draft comes from
 * `GET /cases/{caseId}/patient-message-draft` when the reader asks for it, never on its own,
 * because each request is written for this one case. The AI never sees the patient's name: the API
 * adds the first name to the draft afterwards. A detail the API has no value for, such as
 * `{clinic contact}`, stays a placeholder. The draft is shown as plain text.
 * Messages that reach the patient still come from the approved templates.
 */
export function PatientMessageCard({ detail, reason }: PatientMessageCardProps) {
  const headingId = useId()
  const channelName = useId()
  const draftRef = useRef<HTMLDivElement>(null)
  const [channel, setChannel] = useState<MessageChannel>('Sms')
  const [busy, setBusy] = useState(false)
  const [draft, setDraft] = useState<AiText>()
  const [failure, setFailure] = useState<FailureKind>()

  // Move to the draft once it is on the page, so a screen reader reads it where the reader asked for it.
  useEffect(() => {
    if (draft) {
      draftRef.current?.focus()
    }
  }, [draft])

  const write = async (): Promise<void> => {
    if (busy) {
      return
    }
    setBusy(true)
    setFailure(undefined)
    const outcome = await draftPatientMessage(detail.caseId, channel, reason)
    setBusy(false)
    if (outcome.ok) {
      setDraft(outcome.data)
      return
    }
    setDraft(undefined)
    setFailure(outcome.failure.kind)
  }

  return (
    <section className="card message-card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('patientMessage.title')}
      </h2>
      <p className="sub-line">{t('patientMessage.lede')}</p>
      <fieldset className="message-channel">
        <legend className="visually-hidden">{t('patientMessage.channel.legend')}</legend>
        {CHANNELS.map((option) => (
          <label className="radio" key={option.value}>
            <input
              type="radio"
              name={channelName}
              checked={channel === option.value}
              onChange={() => setChannel(option.value)}
            />
            {t(option.label)}
          </label>
        ))}
      </fieldset>
      <button type="button" className="btn" aria-disabled={busy} onClick={() => void write()}>
        {busy ? t('patientMessage.writing') : draft ? t('patientMessage.again') : t('patientMessage.write')}
      </button>
      {failure ? (
        <p className="field-error" role="alert">
          {t(FAILURES[failure] ?? 'patientMessage.failed.other')}
        </p>
      ) : null}
      {draft ? (
        <div className="message-draft" ref={draftRef} tabIndex={-1} role="group" aria-label={t('patientMessage.draftLabel')}>
          <p className="message-draft-text">{draft.text}</p>
          <p className="ai-brief-meta muted">
            <Icon name="spark" />
            <span>
              {t('ai.writtenByAi')}
              {t('common.separator')}
              {formatTime(new Date(draft.generatedAt))}
              {t('common.separator')}
              {t('patientMessage.notSent')}
            </span>
          </p>
        </div>
      ) : null}
    </section>
  )
}
