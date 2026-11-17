import { useProgramme, type CaseDetail, type TimelineEntry } from '../../data'
import {
  describeOwner,
  escalationLadder,
  escalationTarget,
  isAddressedTo,
  noticeActor,
  type LadderRung,
} from '../../domain'
import { formatWhen, parseInstant } from '../../format'
import { roleLabel, sameReference, useSignedInStaff, type StaffMember } from '../../identity'
import { t } from '../../i18n'

/** Props for {@link EscalationLadder}. */
export interface EscalationLadderProps {
  /** The case, with its escalations. */
  readonly detail: CaseDetail
  /** The case's timeline, when it has loaded, to say when the reminder went. */
  readonly timeline?: readonly TimelineEntry[]
  /** The current time. */
  readonly now: Date
}

function rungTitle(rung: LadderRung, detail: CaseDetail, me: StaffMember): string {
  if (rung.kind === 'reminder') {
    const isMe = !detail.owner.isQueue && sameReference(detail.owner.reference, me.reference)
    return isMe
      ? t('escalation.rungs.reminderYou')
      : t('escalation.rungs.reminder', { name: describeOwner(detail.owner).inSentence })
  }
  if (isAddressedTo(rung.target, me)) {
    return t('escalation.rungs.you', { role: (roleLabel(rung.target.role) ?? rung.target.role).toLocaleLowerCase() })
  }
  return escalationTarget(rung.target).name
}

function rungDetail(rung: LadderRung, now: Date, me: StaffMember): string {
  const when = rung.at ? formatWhen(rung.at, now) : undefined
  if (rung.kind === 'reminder') {
    if (rung.sent) {
      return when ? t('escalation.rungs.reminderSent', { date: when }) : t('escalation.rungs.reminderSentNoDate')
    }
    return when ? t('escalation.rungs.reminderDue', { date: when }) : t('escalation.rungs.reminderBefore')
  }
  if (rung.state === 'upcoming') {
    return when ? t('escalation.rungs.upcoming', { date: when }) : t('escalation.rungs.upcomingNoDate')
  }
  const escalation = rung.escalation
  const raised = when ? t('escalation.rungs.raised', { date: when }) : t('escalation.rungs.raisedNoDate')
  const acknowledged = parseInstant(escalation?.acknowledgedAt)
  if (escalation && acknowledged) {
    const name = noticeActor(
      { reference: escalation.acknowledgedBy ?? '', role: escalation.acknowledgedByRole ?? escalation.escalatedTo.role },
      me.reference,
    )
    return `${raised} ${t('escalation.rungs.acknowledged', { name, date: formatWhen(acknowledged, now) })}`
  }
  if (rung.state === 'current') {
    const ask = isAddressedTo(rung.target, me)
    return `${raised} ${ask ? t('escalation.rungs.pleaseAcknowledge') : t('escalation.rungs.notAcknowledged')}`
  }
  return raised
}

/**
 * The escalation ladder of a case: the reminder to the person responsible before the deadline, then
 * each level of the programme's ladder, with the current one marked (boards 2 and 17).
 *
 * @remarks
 * Reads `GET /programmes/{pathwayId}` for the ladder and the reminder time; while it loads or if it
 * fails, the ladder shows the escalations the case has, and if it fails a calm note says the
 * programme's steps are not available, so a missing later level never looks like the end of the
 * ladder. Nothing else on the screen depends on the programme. Done rungs say when they happened
 * and who acknowledged them, the current rung asks the role it went to for an acknowledgement, and
 * later rungs say when they will happen if the step is still late. Escalations go to a role at a
 * facility, never to a named person.
 */
export function EscalationLadder({ detail, timeline, now }: EscalationLadderProps) {
  const me = useSignedInStaff()
  const programme = useProgramme(detail.pathwayId)
  const rungs = escalationLadder({
    detail,
    programme: programme.state.status === 'ready' ? programme.state.data : undefined,
    timeline,
    now,
  })
  return (
    <>
      <ol className="ladder" aria-label={t('escalation.ladderLabel')}>
        {rungs.map((rung, index) => (
          <li
            key={rung.key}
            className={`rung ${rung.state}`}
            aria-current={rung.state === 'current' ? 'step' : undefined}
          >
            <span className="rung-n" aria-hidden="true">
              {index + 1}
            </span>
            <span className="rung-text">
              <b>{rungTitle(rung, detail, me)}</b>
              <span>{rungDetail(rung, now, me)}</span>
              <span className="visually-hidden">{t(`escalation.rungStates.${rung.state}`)}</span>
            </span>
          </li>
        ))}
      </ol>
      {programme.state.status === 'failed' ? <p className="note linkrow">{t('escalation.ladderPartial')}</p> : null}
    </>
  )
}
