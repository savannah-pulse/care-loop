import type { CallOutcome, CaseDetail } from '../../data'
import { isCallOutcomeRole } from '../../identity'

/** The lowest confidence rating a patient can give. */
export const MIN_CONFIDENCE = 1

/** The highest confidence rating a patient can give. */
export const MAX_CONFIDENCE = 5

/** The ratings a nurse can choose from, lowest first. */
export const CONFIDENCE_RATINGS: readonly number[] = Array.from(
  { length: MAX_CONFIDENCE - MIN_CONFIDENCE + 1 },
  (_, index) => MIN_CONFIDENCE + index,
)

/** What a nurse has ticked on the call-outcome card and not yet recorded. */
export interface CallOutcomeDraft {
  /** The patient said they will come. */
  readonly willCome: boolean
  /** The patient said the next step back in their own words. */
  readonly saidBack: boolean
  /** How confident the patient said they feel, from 1 to 5, or `undefined` when not asked. */
  readonly confidence?: number
}

/** A card with nothing ticked. */
export const EMPTY_CALL_OUTCOME: CallOutcomeDraft = { willCome: false, saidBack: false }

/**
 * Whether the signed-in person is offered the call-outcome card on a case.
 *
 * - `hidden`: not a nurse navigator, or the case is closed.
 * - `notYet`: a nurse navigator, but no message has been sent to the patient, so the API would
 *   refuse an outcome.
 * - `open`: a nurse navigator on a case whose patient has been sent a message.
 */
export type CallOutcomeOffer = 'hidden' | 'notYet' | 'open'

/**
 * Decides whether to offer the call-outcome card.
 *
 * @param detail - The case.
 * @param role - The signed-in person's role.
 * @returns See {@link CallOutcomeOffer}. The API decides in the end; this only keeps the screen
 * from offering what it would refuse.
 */
export function callOutcomeOffer(detail: Pick<CaseDetail, 'stage' | 'tracks'>, role: string): CallOutcomeOffer {
  if (!isCallOutcomeRole(role) || detail.stage === 'Closed') {
    return 'hidden'
  }
  const contact = detail.tracks.find((track) => track.kind === 'Notification')
  return contact?.status ? 'open' : 'notYet'
}

/**
 * Tells whether a draft records anything.
 *
 * @param draft - What is ticked.
 * @returns `true` when at least one outcome is recorded and any rating is a whole number from 1 to 5.
 */
export function canRecord(draft: CallOutcomeDraft): boolean {
  const rated = draft.confidence !== undefined
  if (rated && !CONFIDENCE_RATINGS.includes(draft.confidence)) {
    return false
  }
  return draft.willCome || draft.saidBack || rated
}

/**
 * Turns a draft into what the API is sent.
 *
 * @param draft - What is ticked.
 * @returns The request body. A rating that was not given is sent as `null`, never as a number.
 */
export function toCallOutcome(draft: CallOutcomeDraft): CallOutcome {
  return { acknowledged: draft.willCome, understood: draft.saidBack, confidence: draft.confidence ?? null }
}

/**
 * Reads the confidence rating on a case.
 *
 * @param detail - The case.
 * @returns The rating as a whole number from 1 to 5, or `undefined` when none is recorded.
 */
export function recordedConfidence(detail: Pick<CaseDetail, 'confidence'>): number | undefined {
  const value = Number(detail.confidence ?? Number.NaN)
  return CONFIDENCE_RATINGS.includes(value) ? value : undefined
}
