import { useId } from 'react'
import { Badge, Reference } from '../../components'
import type { CaseDetail } from '../../data'
import {
  caseStatus,
  describeOwner,
  exceptionReason,
  isExceptionName,
  nextActionText,
} from '../../domain'
import { describeDue, formatWhen, parseInstant } from '../../format'
import { t } from '../../i18n'

/** Props for {@link NextActionCard}. */
export interface NextActionCardProps {
  /** The case, from `GET /cases/{caseId}`. */
  readonly detail: CaseDetail
  /** The current time, for how late or how long is left. */
  readonly now: Date
}

function reasons(detail: CaseDetail): string[] {
  const sentences = detail.exceptions.filter(isExceptionName).map(exceptionReason)
  if (detail.proposedOwner) {
    sentences.push(
      t('case.proposedOwner', {
        name: describeOwner(detail.proposedOwner).name,
        owner: describeOwner(detail.owner).name,
      }),
    )
  }
  return sentences
}

/**
 * The case's single next action: what must happen, who is responsible, when it is due and whether
 * it has been escalated.
 *
 * @remarks
 * The status badge uses the same rule as the worklist. The explanation comes from the unresolved
 * exceptions and any pending transfer: until a proposed owner accepts, the current owner stays
 * responsible. Escalation brings in oversight but never changes the owner.
 */
export function NextActionCard({ detail, now }: NextActionCardProps) {
  const headingId = useId()
  const status = caseStatus(detail)
  const owner = describeOwner(detail.owner)
  const due = parseInstant(detail.dueAt)
  const dueText = due ? describeDue(due, now) : undefined
  const why = reasons(detail)
  return (
    <section className="card next" aria-labelledby={headingId}>
      <div className="top">
        <span className="lbl">{t('case.nextActionLabel')}</span>
        <Badge tone={status.tone}>{status.label}</Badge>
        {detail.isEscalated ? <Badge tone="grey">{t('status.escalated')}</Badge> : null}
      </div>
      <h2 id={headingId}>{nextActionText(detail.nextAction)}</h2>
      <p className="why">{why.length > 0 ? why.join(' ') : t('case.noProblem')}</p>
      <div className="kv">
        <div>
          <small>{t('case.responsible')}</small>
          <b>{owner.name}</b>
          <span>{owner.detailIsReference ? <Reference value={owner.detail} /> : owner.detail}</span>
        </div>
        <div>
          <small>{dueText?.overdue ? t('case.wasDue') : t('case.due')}</small>
          {due && dueText ? (
            <>
              <b className={dueText.overdue ? 'red' : undefined}>{formatWhen(due, now)}</b>
              <span>{dueText.overdue ? dueText.primary : dueText.secondary}</span>
            </>
          ) : (
            <b>{t('time.noDueTime')}</b>
          )}
        </div>
        <div>
          <small>{t('case.escalation')}</small>
          <b>{detail.isEscalated ? t('case.escalationOpen') : t('case.escalationNone')}</b>
          {detail.isEscalated ? <span>{t('case.escalationOpenDetail')}</span> : null}
        </div>
      </div>
    </section>
  )
}
