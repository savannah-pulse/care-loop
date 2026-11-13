import { useId } from 'react'
import { Badge, Icon } from '../../components'
import type { CaseDetail } from '../../data'
import {
  caseStatus,
  describeOwner,
  exceptionReason,
  isExceptionName,
  isReferralAction,
  isUrgent,
  nextActionText,
} from '../../domain'
import { describeDue, parseInstant } from '../../format'
import { isClinicianRole, useSignedInStaff } from '../../identity'
import { t } from '../../i18n'
import { routeHref } from '../../routing'

/** Props for {@link NextActionCard}. */
export interface NextActionCardProps {
  /** The case, from `GET /cases/{caseId}`. */
  readonly detail: CaseDetail
  /** The current time, for how late or how long is left. */
  readonly now: Date
}

function why(detail: CaseDetail): string {
  const sentences = detail.exceptions
    .filter(isExceptionName)
    .filter((name) => name !== 'Overdue')
    .map(exceptionReason)
  if (detail.proposedOwner) {
    sentences.push(
      t('case.proposedOwner', {
        name: describeOwner(detail.proposedOwner).inSentence,
        owner: describeOwner(detail.owner).subject,
      }),
    )
  }
  if (sentences.length === 0 && detail.exceptions.includes('Overdue')) {
    sentences.push(exceptionReason('Overdue'))
  }
  return sentences.length > 0 ? sentences.join(' ') : t('case.noProblem')
}

/**
 * The case's single next action, shown as the main card of the case screen: what must happen,
 * why, who is responsible, when it is due and whether it has been escalated.
 *
 * @remarks
 * The status pill uses the same rule as the worklist. The explanation comes from the unresolved
 * exceptions and any pending transfer: until a proposed owner accepts, the current owner stays
 * responsible. Escalation tells the facility lead but never changes who is responsible. Clinicians
 * get a way into the handoff summary when the action is to send or arrange a referral; everyone
 * else sees who needs to act.
 */
export function NextActionCard({ detail, now }: NextActionCardProps) {
  const headingId = useId()
  const me = useSignedInStaff()
  const status = caseStatus(detail)
  const owner = describeOwner(detail.owner)
  const dueAt = parseInstant(detail.dueAt)
  const due = dueAt ? describeDue(dueAt, now) : undefined
  const closed = detail.stage === 'Closed'
  const offerSummary = !closed && isClinicianRole(me.role) && isReferralAction(detail.nextAction)
  const mine = !detail.owner.isQueue && detail.owner.reference === me.reference
  return (
    <section className="card hero" aria-labelledby={headingId}>
      <div className="hero-top">
        <span className="lbl">{t('case.nextActionLabel')}</span>
        <span className="pills">
          <Badge tone={status.tone}>{status.label}</Badge>
          {status.kind !== 'urgent' && isUrgent(detail.urgency) ? <Badge tone="red">{t('status.urgent')}</Badge> : null}
          {detail.isEscalated ? <Badge tone="grey">{t('status.escalated')}</Badge> : null}
        </span>
      </div>
      <h2 className="hero-title" id={headingId}>
        {nextActionText(detail.nextAction)}
      </h2>
      <p className="why">{why(detail)}</p>
      {detail.closureReason ? (
        <p className="closed-line">{t('case.closedAs', { reason: t(`closureReasons.${detail.closureReason}`) })}</p>
      ) : null}
      {detail.outcomeEvidenceReference ? (
        <p className="closed-line">{t('case.outcomeEvidence', { reference: detail.outcomeEvidenceReference })}</p>
      ) : null}
      <dl className="facts">
        <div>
          <dt>{t('case.responsible')}</dt>
          <dd title={owner.reference}>
            <span className="fact-main">{owner.name}</span>
            {owner.detail ? <span className="fact-sub">{owner.detail}</span> : null}
          </dd>
        </div>
        <div>
          <dt>{t('case.due')}</dt>
          <dd>
            {due ? (
              <>
                <span className={due.overdue ? 'fact-main red' : 'fact-main'}>{due.primary}</span>
                <span className="fact-sub">{due.secondary}</span>
              </>
            ) : (
              <span className="fact-main">{t('time.noDueTime')}</span>
            )}
          </dd>
        </div>
        <div className="fact-wide">
          <dt>{t('case.escalation')}</dt>
          <dd>
            {detail.isEscalated ? (
              <span className="fact-plain">{t('case.escalationOpen')}</span>
            ) : (
              <span className="fact-main">{t('case.escalationNone')}</span>
            )}
          </dd>
        </div>
      </dl>
      {offerSummary ? (
        <div className="hero-cta">
          <a className="btn primary" href={routeHref({ name: 'summary', caseId: detail.caseId })}>
            <Icon name="spark" />
            {t('case.reviewSummary')}
          </a>
        </div>
      ) : null}
      {!offerSummary && !closed ? (
        <p className="who-acts">
          <Icon name="user" />
          <span>{mine ? t('case.youAct') : t('case.otherActs', { name: owner.subject })}</span>
        </p>
      ) : null}
    </section>
  )
}
