import type { HandoffSummary } from '../../data'
import { getSignedInStaff, onSignedInChange } from '../../identity'
import type { SectionEdits } from './summary'

/**
 * A handoff summary draft that is still open: what AI drafted, and what the reviewer has changed so
 * far.
 */
export interface OpenDraft {
  /** The `id` of the person reviewing it: whoever was signed in when it was drafted. */
  readonly reviewerId: string
  /** The draft exactly as the API returned it: its identifier, its sections and its original text. */
  readonly summary: HandoffSummary
  /** The reviewer's edited section texts, by section position, exactly as typed. */
  readonly edits: SectionEdits
}

const openDrafts = new Map<string, OpenDraft>()

/**
 * Keeps a case's open draft, with the reviewer's edits so far, so that leaving the summary page
 * (for example to check the case's timeline) and coming back does not lose it.
 *
 * @param caseId - The case the draft is for.
 * @param draft - The draft and the reviewer's edits.
 *
 * @remarks
 * The API has no way to read a draft back, so this is the only copy until the reviewer approves or
 * rejects it. It is kept in memory only, one draft per case: clinical text is never written to
 * browser storage, so reloading the page loses it. A summary that is no longer a draft is not kept,
 * and neither is one drafted for a person who is no longer signed in, such as a draft that arrives
 * after someone else signed in.
 */
export function keepOpenDraft(caseId: string, draft: OpenDraft): void {
  if (draft.summary.status !== 'Draft' || draft.reviewerId !== getSignedInStaff().id) {
    return
  }
  openDrafts.set(caseId, draft)
}

/**
 * Finds the draft the signed-in person left open on a case.
 *
 * @param caseId - The case.
 * @returns The open draft with the edits so far, or `undefined` when the person signed in now has
 * none on this case.
 */
export function openDraftFor(caseId: string): OpenDraft | undefined {
  const draft = openDrafts.get(caseId)
  return draft?.reviewerId === getSignedInStaff().id ? draft : undefined
}

/**
 * Forgets a case's open draft: once it is approved or rejected, or the API says it was decided or
 * replaced elsewhere.
 *
 * @param caseId - The case.
 */
export function forgetOpenDraft(caseId: string): void {
  openDrafts.delete(caseId)
}

/**
 * Forgets every open draft.
 *
 * @remarks
 * Runs whenever someone signs in as another person, so nobody sees, edits or approves a draft that
 * someone else was working on.
 */
export function forgetOpenDrafts(): void {
  openDrafts.clear()
}

onSignedInChange(forgetOpenDrafts)
