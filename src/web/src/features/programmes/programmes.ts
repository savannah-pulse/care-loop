import { LOCALE } from '../../i18n'
import { parseTimeSpan, formatSpan, humaniseId } from '../../format'
import type { DeadlineDefinition, EscalationStep, Programme } from '../../data'

/** A text from a programme's settings, with any placeholder marker taken off the front. */
export interface SettingText {
  /** The text without the marker, starting with a capital letter. */
  readonly text: string
  /** `true` when the settings marked the text as a placeholder. */
  readonly placeholder: boolean
}

const PLACEHOLDER_MARKER = /^\s*PLACEHOLDER\s*[-\u2013\u2014:]\s*/i

/**
 * Takes the placeholder marker off the front of a text from a programme's settings.
 *
 * @param value - A name, citation, description or approver, for example
 * `PLACEHOLDER - no clinical source yet`.
 * @returns The text without a leading `PLACEHOLDER -`, starting with a capital letter, and whether
 * the marker was there. Nothing else in the text changes.
 *
 * @remarks
 * The programme files mark every value a clinical adviser has not approved yet with this marker in
 * capitals. The screen says "Placeholder" or "Waiting for approval" in words instead, so the marker
 * is not repeated on every line and nothing is lost.
 */
export function settingText(value: string): SettingText {
  const marker = PLACEHOLDER_MARKER.exec(value)
  if (!marker) {
    return { text: value, placeholder: false }
  }
  const rest = value.slice(marker[0].length)
  return { text: `${rest.charAt(0).toLocaleUpperCase(LOCALE)}${rest.slice(1)}`, placeholder: true }
}

/**
 * Finds the one citation shared by every item, so the screen can say it once.
 *
 * @param citations - The citation of each item, as the settings give it.
 * @returns The citation when there are items and all have the same one; otherwise `undefined`, and
 * each item shows its own.
 */
export function sharedCitation(citations: readonly (string | null | undefined)[]): string | undefined {
  const first = citations[0]
  return first && citations.every((citation) => citation === first) ? first : undefined
}

/**
 * Lists the different citations of a list of items, each once, in order of first appearance.
 *
 * @param citations - The citation of each item, as the settings give it.
 * @returns The distinct citations, so the screen can list them once under a table and give each
 * row the number of its source instead of repeating the text.
 */
export function distinctCitations(citations: readonly (string | null | undefined)[]): string[] {
  return [...new Set(citations.filter((citation): citation is string => Boolean(citation?.trim())))]
}

/**
 * Whether a programme's settings are approved, as one word for the programme list.
 *
 * @remarks
 * - `approved`: a clinical adviser approved every deadline, ladder step, closure rule and kind of
 *   evidence, and the definition is not marked as a placeholder.
 * - `waiting`: something is still a placeholder waiting for approval.
 */
export type ProgrammeApproval = 'approved' | 'waiting'

/**
 * Works out whether a programme's settings are approved.
 *
 * @param programme - The programme's settings.
 * @returns `waiting` when the definition is a placeholder or any deadline, ladder step or kind of
 * evidence is not clinically approved; `approved` otherwise.
 */
export function programmeApproval(
  programme: Pick<Programme, 'isPlaceholder' | 'deadlines' | 'escalationLadder' | 'evidenceKinds'>,
): ProgrammeApproval {
  const waiting =
    programme.isPlaceholder ||
    programme.deadlines.some((deadline) => !deadline.clinicallyApproved) ||
    programme.escalationLadder.some((step) => !step.clinicallyApproved) ||
    programme.evidenceKinds.some((kind) => !kind.clinicallyApproved)
  return waiting ? 'waiting' : 'approved'
}

/**
 * Formats a duration from a programme's settings exactly.
 *
 * @param value - The duration as the API writes it, for example `1.00:00:00` or `00:00:48`.
 * @returns For example `1 day`, `4 h` or `48 s`; the value itself when it is not a duration, so
 * nothing is made up.
 */
export function settingDuration(value: string): string {
  const milliseconds = parseTimeSpan(value)
  return milliseconds === undefined ? value : formatSpan(milliseconds)
}

/**
 * Tells whether a duration from a programme's settings is zero.
 *
 * @param value - The duration as the API writes it.
 * @returns `true` for a zero duration, such as a ladder step reached at the deadline itself.
 */
export function isZeroDuration(value: string): boolean {
  return parseTimeSpan(value) === 0
}

/**
 * Orders a programme's escalation ladder by level.
 *
 * @param steps - The ladder as the API sends it.
 * @returns A new array, lowest level first.
 */
export function ladderInOrder(steps: readonly EscalationStep[]): EscalationStep[] {
  return [...steps].sort((a, b) => Number(a.level) - Number(b.level))
}

/**
 * Words a kind of outcome evidence, such as `treatment-plan`.
 *
 * @param kind - The code the outcome carries.
 * @returns For example `Treatment plan`.
 */
export function evidenceKindName(kind: string): string {
  return humaniseId(kind)
}

/**
 * Builds a stable key for a deadline row, which can repeat a next action for another urgency.
 *
 * @param deadline - One of the programme's deadlines.
 * @returns The next action and urgency code together.
 */
export function deadlineKey(deadline: Pick<DeadlineDefinition, 'nextAction' | 'urgencyCode'>): string {
  return `${deadline.nextAction}|${deadline.urgencyCode ?? ''}`
}
