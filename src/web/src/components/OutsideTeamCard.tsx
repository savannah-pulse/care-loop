import { useId } from 'react'
import { t } from '../i18n'
import { AccessReasonForm } from './AccessReasonForm'

/** Props for {@link OutsideTeamCard}. */
export interface OutsideTeamCardProps {
  /** Called with the reason the person gave; the screen then reads the case with it. */
  readonly onReason: (reason: string) => void
  /** Called when the person decides not to open the case. */
  readonly onCancel: () => void
}

/**
 * "Opening a case outside your team" (board 14): asks why the person needs a case another team
 * cares for, before it opens.
 *
 * @remarks
 * Shown when the API refuses a case read because the case is at another facility and no reason
 * was given. The patient is not named, because nothing about the case is shown until the reason is
 * given. The options do not assume who the patient is.
 */
export function OutsideTeamCard({ onReason, onCancel }: OutsideTeamCardProps) {
  const headingId = useId()
  return (
    <section className="card reason-card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('access.caseTitle')}
      </h2>
      <p className="reason-lead">{t('access.caseLead')}</p>
      <AccessReasonForm
        intro={t('access.caseIntro')}
        options={[t('access.caseReasons.calledForHelp'), t('access.caseReasons.covering')]}
        submitLabel={t('access.openCase')}
        onSubmit={onReason}
        onCancel={onCancel}
      />
    </section>
  )
}
