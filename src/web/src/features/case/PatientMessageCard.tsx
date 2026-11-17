import { useId, useState } from 'react'
import { AiWords, Dialog, Icon, LoadingState } from '../../components'
import { usePatientMessageDraft, type MessageChannel } from '../../data'
import { t } from '../../i18n'

/** Props for {@link PatientMessageCard}. */
export interface PatientMessageCardProps {
  /** CareLoop's identifier of the case the message would be about. */
  readonly caseId: string
  /** Why the reader opened a case outside their team, when they gave one; sent with the request. */
  readonly reason?: string
}

/** The channels a preview can be written for, in the order the choice offers them. */
const CHANNELS: readonly MessageChannel[] = ['Sms', 'Email']

interface PreviewProps {
  readonly caseId: string
  readonly reason?: string
  readonly onClose: () => void
}

function Preview({ caseId, reason, onClose }: PreviewProps) {
  const ids = useId()
  const [channel, setChannel] = useState<MessageChannel>('Sms')
  const { state, retry } = usePatientMessageDraft({ caseId, channel, reason })
  const kind = state.status === 'failed' ? state.failure.kind : undefined
  const canAskAgain = state.status === 'ready' || (kind !== undefined && kind !== 'notAvailable' && kind !== 'forbidden')
  return (
    <>
      <fieldset className="channel-choice">
        <legend>{t('case.message.channelLegend')}</legend>
        {CHANNELS.map((option) => (
          <label key={option} className="radio">
            <input
              type="radio"
              name={`${ids}-channel`}
              value={option}
              checked={channel === option}
              onChange={() => setChannel(option)}
            />
            <span>{t(`case.message.channels.${option}`)}</span>
          </label>
        ))}
      </fieldset>
      <div className="preview-body">
        {state.status === 'loading' ? <LoadingState label={t('case.message.loading')} /> : null}
        {state.status === 'ready' ? (
          <AiWords text={state.data.text} caption={t('case.message.caption')} chip={t('ai.draftChip')} className="preview" />
        ) : null}
        {kind === 'notAvailable' ? (
          <p className="ai-quiet" role="status">
            {t('case.message.notReady')}
          </p>
        ) : null}
        {kind === 'unavailable' ? (
          <p className="ai-note" role="status">
            <Icon name="alert" />
            <span>{t('case.message.failed')}</span>
          </p>
        ) : null}
        {kind === 'forbidden' ? (
          <p className="ai-quiet" role="status">
            {t('case.message.forbidden')}
          </p>
        ) : null}
        {kind !== undefined && kind !== 'notAvailable' && kind !== 'unavailable' && kind !== 'forbidden' ? (
          <p className="ai-note" role="status">
            <Icon name="alert" />
            <span>{t('case.message.error')}</span>
          </p>
        ) : null}
      </div>
      <div className="dialog-actions">
        {canAskAgain ? (
          <button type="button" className="btn" onClick={retry}>
            {t('case.message.again')}
          </button>
        ) : null}
        <button type="button" className="btn primary" onClick={onClose}>
          {t('case.message.close')}
        </button>
      </div>
    </>
  )
}

/**
 * "Message to the patient": for the nurse navigator and the clinician, a way to preview how the
 * patient-notification agent would word a text message or an email about the case's next step.
 *
 * @remarks
 * The button opens a dialog that asks `POST /cases/{caseId}/patient-message-draft` for a preview, as
 * a text message first; the person can switch to an email or ask again. The draft is captioned as
 * written by AI and not sent: patient messages go out from approved templates, and this preview is
 * neither sent nor stored. The agent is given no name: it writes `{first name}` where the name goes.
 * While AI is switched off (501) the dialog says so in one calm line; when the agent could not write
 * it (503) it says nothing was sent. Focus moves into the dialog when it opens and back to the button
 * when it closes.
 */
export function PatientMessageCard({ caseId, reason }: PatientMessageCardProps) {
  const headingId = useId()
  const [open, setOpen] = useState(false)
  return (
    <section className="card message-card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('case.message.title')}
      </h2>
      <p className="sub-line">{t('case.message.intro')}</p>
      <button type="button" className="btn" onClick={() => setOpen(true)}>
        <Icon name="spark" />
        {t('case.message.preview')}
      </button>
      <Dialog open={open} title={t('case.message.dialogTitle')} onClose={() => setOpen(false)}>
        {open ? <Preview caseId={caseId} reason={reason} onClose={() => setOpen(false)} /> : null}
      </Dialog>
    </section>
  )
}
