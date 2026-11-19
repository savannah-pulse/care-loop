import { useId, type ReactNode } from 'react'
import { Badge, Icon } from '../../components'
import type { CaseDetail } from '../../data'
import {
  caseStatus,
  currentEscalation,
  describeOwner,
  escalationTarget,
  isUrgent,
  nextActionText,
  nextActionWhy,
  noticeActor,
  outcomeEvidenceText,
} from '../../domain'
import { describeDue, formatWhen, parseInstant } from '../../format'
import { isEscalationRole, sameReference, useSignedInStaff } from '../../identity'
import { t } from '../../i18n'
import { routeHref } from '../../routing'

/** Props for {@link NextActionCard}. */
export interface NextActionCardProps {
  /** The case, from `GET /cases/{caseId}`. */
  readonly detail: CaseDetail
  /** The current time, for how late or how long is left. */
  readonly now: Date
  /** The escalation ladder, shown under the facts while the case is escalated. */
  readonly children?: ReactNode
}

interface EscalationFactProps {
  readonly detail: CaseDetail
  readonly now: Date
}

function EscalationFact({ detail, now }: EscalationFactProps) {
  const me = useSignedInStaff()
  const current = currentEscalation(detail.escalations)
  if (!current) {
    return <span className="fact-main">{t('case.escalationNone')}</span>
  }
  const raised = parseInstant(current.raisedAt)
  const acknowledged = parseInstant(current.acknowledgedAt)
  const target = escalationTarget(current.escalatedTo)
  return (
    <>
      <span className="fact-plain">
        {raised
          ? t('case.escalatedTo', { target: target.inSentence, date: formatWhen(raised, now) })
          : t('case.escalatedToNoDate', { target: target.inSentence })}{' '}
        {acknowledged
          ? t('case.escalationAcknowledged', {
              name: noticeActor(
                { reference: current.acknowledgedBy ?? '', role: current.acknowledgedByRole ?? current.escalatedTo.role },
                me.reference,
              ),
              date: formatWhen(acknowledged, now),
            })
          : t('case.escalationNotAcknowledged')}{' '}
        {t('case.escalationSameOwner')}
      </span>
      {isEscalationRole(me.role) ? (
        <a className="fact-link" href={routeHref({ name: 'escalation', caseId: detail.caseId })}>
          {t('case.openEscalation')}
          <Icon name="right" />
        </a>
      ) : null}
    </>
  )
}

/**
 * The case's single next action, shown as the main card of the case screen: what must happen,
 * why, who is responsible, when it is due and whether it has been escalated.
 *
 * @remarks
 * The status pill uses the same rule as the worklist. The explanation comes from the unresolved
 * exceptions and any pending transfer: until a proposed owner accepts, the current owner stays
 * responsible. Escalation brings in the facility lead, then the programme coordinator, but never
 * changes who is responsible; the card says who it went to and whether they acknowledged it, and
 * gives those roles a way into the escalation (screen 17). While the case is open it says who needs
 * to act.
 */
export function NextActionCard({ detail, now, children }: NextActionCardProps) {
  const headingId = useId()
  const me = useSignedInStaff()
  const status = caseStatus(detail)
  const owner = describeOwner(detail.owner)
  const dueAt = parseInstant(detail.dueAt)
  const due = dueAt ? describeDue(dueAt, now) : undefined
  const closed = detail.stage === 'Closed'
  const mine = !detail.owner.isQueue && sameReference(detail.owner.reference, me.reference)
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
      <p className="why">{nextActionWhy(detail)}</p>
      {detail.closureReason ? (
        <p className="closed-line">{t('case.closedAs', { reason: t(`closureReasons.${detail.closureReason}`) })}</p>
      ) : null}
      {detail.outcomeEvidenceReference ? (
        <p className="closed-line">{outcomeEvidenceText(detail.outcomeEvidenceReference, me.role)}</p>
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
            <EscalationFact detail={detail} now={now} />
          </dd>
        </div>
      </dl>
      {children}
      {closed ? null : (
        <p className="who-acts">
          <Icon name="user" />
          <span>{mine ? t('case.youAct') : t('case.otherActs', { name: owner.subject })}</span>
        </p>
      )}
    </section>
  )
}
