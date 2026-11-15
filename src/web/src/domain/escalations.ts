import { t } from '../i18n'
import { facilityName, parseInstant, parseTimeSpan } from '../format'
import { findStaffByReference, roleLabel, sameReference } from '../identity'
import type { CaseDetail, DeadlineDefinition, Escalation, Owner, Programme, TimelineEntry } from '../data'

/**
 * Lists a case's open escalations: those whose late step is not done yet.
 *
 * @param escalations - Every escalation on the case, oldest first, as the API sends them.
 * @returns The open ones, in the same order.
 */
export function openEscalations(escalations: readonly Escalation[]): Escalation[] {
  return escalations.filter((escalation) => !escalation.resolvedAt)
}

/**
 * Finds the current escalation: the most recent one still open.
 *
 * @param escalations - Every escalation on the case, oldest first.
 * @returns The last open escalation, or `undefined` when none is open.
 *
 * @remarks
 * Only the current escalation can be acknowledged. An older one, or one already resolved, is
 * history: acknowledging it now would record that someone saw what they were never shown.
 */
export function currentEscalation(escalations: readonly Escalation[]): Escalation | undefined {
  return openEscalations(escalations).at(-1)
}

/**
 * Tells whether the signed-in person may acknowledge the current escalation of a case.
 *
 * @param detail - The case.
 * @param person - The role the person acts in and the facility they work at, if any.
 * @returns `true` when the case is open, its current escalation is not acknowledged yet, it went to
 * the person's role and, for anyone who works at a facility, the case is at their facility.
 *
 * @remarks
 * The API answers 403 to any other role and 409 when the escalation was acknowledged or replaced
 * meanwhile. It also lets a facility lead from another facility acknowledge by giving a reason; an
 * escalation goes to the lead of the case's facility, so the dashboard offers the button only to
 * them. The programme coordinator works across facilities, so the role is enough.
 */
export function canAcknowledge(
  detail: Pick<CaseDetail, 'stage' | 'escalations' | 'facilityId'>,
  person: { readonly role: string; readonly facilityId?: string },
): boolean {
  const current = currentEscalation(detail.escalations)
  return (
    detail.stage !== 'Closed' &&
    current !== undefined &&
    !current.acknowledgedAt &&
    current.escalatedTo.role.toLowerCase() === person.role.toLowerCase() &&
    (person.facilityId === undefined || person.facilityId === detail.facilityId)
  )
}

/**
 * Tells whether an escalation went to the signed-in person: their role, at their facility.
 *
 * @param target - The role and facility the escalation, or a ladder rung, is addressed to.
 * @param me - The signed-in person.
 * @returns `true` when the role is the person's and, for anyone who works at a facility, the
 * facility is theirs too. The programme coordinator works across facilities, so the role is enough.
 *
 * @remarks
 * The API lets a facility lead from another facility acknowledge if they give a reason; the
 * dashboard offers the acknowledgement only to the facility the case belongs to.
 */
export function isAddressedTo(target: { readonly role: string; readonly facilityId?: string }, me: { readonly role: string; readonly facilityId?: string }): boolean {
  if (target.role.toLowerCase() !== me.role.toLowerCase()) {
    return false
  }
  return me.facilityId === undefined || target.facilityId === me.facilityId
}

/** Who an escalation went to, in words. */
export interface EscalationTarget {
  /** At the start of a line, for example `Facility lead, Family Health Centre`. */
  readonly name: string
  /** Inside a sentence, for example `the facility lead at Family Health Centre`. */
  readonly inSentence: string
}

/**
 * Names who an escalation went to.
 *
 * @param target - The role and facility the escalation was addressed to.
 * @returns The role in words with the facility, except for the programme coordinator, who works
 * across facilities.
 *
 * @remarks
 * Escalations go to a role at a facility, not to a named person, so no name is shown.
 */
export function escalationTarget(target: Pick<Owner, 'role' | 'facilityId'>): EscalationTarget {
  const role = roleLabel(target.role) ?? target.role
  const lower = role.toLocaleLowerCase()
  if (target.role === 'coordinator' || !target.facilityId) {
    return { name: role, inSentence: t('escalation.target.inSentence', { role: lower }) }
  }
  const facility = facilityName(target.facilityId)
  return {
    name: t('escalation.target.name', { role, facility }),
    inSentence: t('escalation.target.inSentenceAt', { role: lower, facility }),
  }
}

/**
 * Finds the deadline a programme sets for a next action.
 *
 * @param programme - The programme's settings.
 * @param nextAction - The case's next action.
 * @param urgency - The case's clinician-reviewed urgency code, if any.
 * @returns The deadline for that urgency, or else the one for every urgency; `undefined` when the
 * programme sets none.
 */
export function deadlineFor(
  programme: Pick<Programme, 'deadlines'> | undefined,
  nextAction: string | null | undefined,
  urgency: string | null | undefined,
): DeadlineDefinition | undefined {
  if (!programme || !nextAction) {
    return undefined
  }
  const forAction = programme.deadlines.filter((deadline) => deadline.nextAction === nextAction)
  return (
    (urgency ? forAction.find((deadline) => deadline.urgencyCode === urgency) : undefined) ??
    forAction.find((deadline) => !deadline.urgencyCode)
  )
}

/** Where one rung of the escalation ladder stands. */
export type RungState = 'done' | 'current' | 'upcoming'

/** One rung of a case's escalation ladder: the reminder to the owner, or one escalation level. */
export interface LadderRung {
  /** A stable key for lists. */
  readonly key: string
  /** `reminder` for the reminder before the deadline, `escalation` for a ladder level. */
  readonly kind: 'reminder' | 'escalation'
  /** The ladder level, for an escalation rung. */
  readonly level?: number
  /** Who this rung tells: the owner, or the role and facility of the level. */
  readonly target: Pick<Owner, 'role' | 'facilityId'> & Partial<Pick<Owner, 'reference' | 'isQueue'>>
  /** Whether it has happened, is where the case is now, or is still to come. */
  readonly state: RungState
  /** When it happened, or when it is due to happen if the step is still late. */
  readonly at?: Date
  /**
   * For the reminder: `true` only when the timeline shows a reminder was sent, so the screen never
   * says it was sent on the strength of the clock alone.
   */
  readonly sent?: boolean
  /** The escalation raised at this level, when there is one. */
  readonly escalation?: Escalation
  /** `false` when a clinical adviser has not approved the level yet. */
  readonly approved?: boolean
}

/** What {@link escalationLadder} works from. */
export interface LadderInput {
  /** The case, with its escalations. */
  readonly detail: Pick<CaseDetail, 'owner' | 'nextAction' | 'urgency' | 'dueAt' | 'escalations'>
  /** The case's programme, for the ladder and the reminder time; the ladder still shows without it. */
  readonly programme?: Pick<Programme, 'deadlines' | 'escalationLadder'>
  /** The case's timeline, to find when the reminder was actually sent. */
  readonly timeline?: readonly TimelineEntry[]
  /** The current time. */
  readonly now: Date
}

const ESCALATION_MATCH_MS = 2000

function sameMoment(a: Date | undefined, b: Date | undefined): boolean {
  return a !== undefined && b !== undefined && Math.abs(a.getTime() - b.getTime()) <= ESCALATION_MATCH_MS
}

function toLevel(value: number | string): number {
  const level = Number(value)
  return Number.isFinite(level) ? level : 0
}

/**
 * Lays out a case's escalation ladder (screen 17): a reminder to the owner before the deadline,
 * then each level of the programme's ladder.
 *
 * @param input - The case, its programme, its timeline and the current time.
 * @returns The rungs in order. Rungs the case has passed are `done`, the current escalation is
 * `current`, and the rest are `upcoming` with when they will happen if the step is still late. The
 * reminder counts as sent only when the timeline records one.
 *
 * @remarks
 * The ladder comes from the programme's settings (`escalationLadder`), never from the dashboard: by
 * default the facility lead at the deadline, then the programme coordinator. Only open escalations
 * count, because a late step that was done resolves every escalation it raised.
 */
export function escalationLadder({ detail, programme, timeline = [], now }: LadderInput): LadderRung[] {
  const open = openEscalations(detail.escalations)
  const current = open.at(-1)
  const due = parseInstant(detail.dueAt)
  const deadline = deadlineFor(programme, detail.nextAction, detail.urgency)
  const warnBefore = parseTimeSpan(deadline?.warnBefore)
  const reminderDue = due && warnBefore !== undefined ? new Date(due.getTime() - warnBefore) : undefined
  const raisedTimes = detail.escalations.map((escalation) => parseInstant(escalation.raisedAt))
  const reminderSent = timeline
    .filter((entry) => entry.eventType === 'DeadlineElapsed' && entry.disposition === 'Applied')
    .map((entry) => parseInstant(entry.sourceTimestamp))
    .filter((at): at is Date => at !== undefined && !raisedTimes.some((raised) => sameMoment(raised, at)))
    .filter((at) => (reminderDue ? at.getTime() >= reminderDue.getTime() - 60_000 : true) && (!due || at <= due))
    .at(-1)
  const reminderPassed = open.length > 0 || reminderSent !== undefined || (reminderDue !== undefined && now >= reminderDue)
  const rungs: LadderRung[] = [
    {
      key: 'reminder',
      kind: 'reminder',
      target: detail.owner,
      state: reminderPassed ? 'done' : 'upcoming',
      at: reminderSent ?? reminderDue,
      sent: reminderSent !== undefined,
      approved: deadline?.clinicallyApproved,
    },
  ]
  const steps = [...(programme?.escalationLadder ?? [])].sort((a, b) => toLevel(a.level) - toLevel(b.level))
  const levels = new Set<number>()
  for (const step of steps) {
    const level = toLevel(step.level)
    levels.add(level)
    const escalation = open.filter((candidate) => toLevel(candidate.level) === level).at(-1)
    const after = parseTimeSpan(step.afterOverdue)
    rungs.push({
      key: `level-${level}`,
      kind: 'escalation',
      level,
      target: escalation?.escalatedTo ?? { role: step.escalateToRole, facilityId: detail.owner.facilityId },
      state: escalation ? (escalation === current ? 'current' : 'done') : 'upcoming',
      at: escalation ? parseInstant(escalation.raisedAt) : due && after !== undefined ? new Date(due.getTime() + after) : undefined,
      escalation,
      approved: step.clinicallyApproved,
    })
  }
  for (const escalation of open) {
    const level = toLevel(escalation.level)
    if (!levels.has(level)) {
      levels.add(level)
      rungs.push({
        key: `level-${level}`,
        kind: 'escalation',
        level,
        target: escalation.escalatedTo,
        state: escalation === current ? 'current' : 'done',
        at: parseInstant(escalation.raisedAt),
        escalation,
      })
    }
  }
  return rungs
}

/** The kind of a line in an escalation's notice history. */
export type NoticeKind = 'reminder' | 'raised' | 'acknowledged' | 'refused' | 'resolved'

/** One line of an escalation's notice history. */
export interface Notice {
  /** A stable key for lists. */
  readonly key: string
  /** What happened. */
  readonly kind: NoticeKind
  /** When it happened. */
  readonly at: Date
  /** The ladder level it concerns, when it concerns one. */
  readonly level?: number
  /** Who the escalation went to, for a raised escalation. */
  readonly target?: Pick<Owner, 'role' | 'facilityId'>
  /** Who acted, for an acknowledgement or a refused attempt. */
  readonly actor?: { readonly reference: string; readonly role: string }
  /** The reason CareLoop recorded, when it says more than the kind. */
  readonly reason?: string
}

/**
 * Builds the notice history of a case's escalations, newest first.
 *
 * @param escalations - Every escalation on the case.
 * @param timeline - The case's timeline, for reminders and refused acknowledgements.
 * @returns Each escalation raised, acknowledged and resolved; each reminder (a `DeadlineElapsed`
 * entry that raised no escalation); and each refused acknowledgement (an `EscalationAcknowledged`
 * entry CareLoop refused), with who tried.
 *
 * @remarks
 * Acknowledgements CareLoop applied are already on their escalation, so their timeline entries are
 * not listed twice. The API's own wording of a refusal is left out: it is written for the audit
 * trail, with raw references and timestamps.
 */
export function escalationNotices(escalations: readonly Escalation[], timeline: readonly TimelineEntry[] = []): Notice[] {
  const notices: Notice[] = []
  for (const escalation of escalations) {
    const level = toLevel(escalation.level)
    const raised = parseInstant(escalation.raisedAt)
    if (raised) {
      notices.push({ key: `${escalation.escalationId}-raised`, kind: 'raised', at: raised, level, target: escalation.escalatedTo })
    }
    const acknowledged = parseInstant(escalation.acknowledgedAt)
    if (acknowledged) {
      notices.push({
        key: `${escalation.escalationId}-acknowledged`,
        kind: 'acknowledged',
        at: acknowledged,
        level,
        actor: {
          reference: escalation.acknowledgedBy ?? '',
          role: escalation.acknowledgedByRole ?? escalation.escalatedTo.role,
        },
      })
    }
    const resolved = parseInstant(escalation.resolvedAt)
    if (resolved) {
      notices.push({ key: `${escalation.escalationId}-resolved`, kind: 'resolved', at: resolved, level })
    }
  }
  const raisedTimes = escalations.map((escalation) => parseInstant(escalation.raisedAt))
  for (const entry of timeline) {
    const at = parseInstant(entry.sourceTimestamp)
    if (!at) {
      continue
    }
    const reason = entry.reason.trim() || undefined
    if (entry.eventType === 'DeadlineElapsed' && entry.disposition === 'Applied') {
      if (!raisedTimes.some((raised) => sameMoment(raised, at))) {
        notices.push({ key: entry.auditEventId, kind: 'reminder', at, reason })
      }
    } else if (entry.eventType === 'EscalationAcknowledged' && entry.disposition === 'Rejected') {
      notices.push({
        key: entry.auditEventId,
        kind: 'refused',
        at,
        actor: { reference: entry.actorReference, role: entry.actorRole },
      })
    }
  }
  return notices.sort((a, b) => b.at.getTime() - a.at.getTime())
}

/**
 * Names the person who acknowledged an escalation or tried to.
 *
 * @param actor - Their reference and role, from the API.
 * @param meReference - The signed-in person's reference, to say "you".
 * @returns `you` for the signed-in person, a name the dashboard knows, or the role in words, such
 * as `a facility lead`.
 */
export function noticeActor(actor: { readonly reference: string; readonly role: string }, meReference: string): string {
  if (sameReference(actor.reference, meReference)) {
    return t('escalation.you')
  }
  const known = findStaffByReference(actor.reference)
  if (known) {
    return known.name
  }
  return t('escalation.someone', { role: (roleLabel(actor.role) ?? actor.role).toLocaleLowerCase() })
}
