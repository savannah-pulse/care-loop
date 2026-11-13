import { api } from './client'
import { callApi, type ApiOutcome } from './outcome'
import type { HandoffSummary } from './types'

/**
 * Asks AI to draft a handoff summary for a case.
 *
 * @param caseId - The case to summarise.
 * @returns The new draft (HTTP 201). Fails with `conflict` when AI is off or a summary is already
 * approved, `unavailable` when AI could not produce a valid draft and `notAvailable` while the
 * endpoint is not built.
 *
 * @remarks
 * Side effect: posts to `POST /cases/{caseId}/handoff-summary`, which replaces any unapproved draft.
 * The draft is never used until a person approves it.
 */
export function draftHandoffSummary(caseId: string): Promise<ApiOutcome<HandoffSummary>> {
  return callApi(() => api.POST('/cases/{caseId}/handoff-summary', { params: { path: { caseId } } }))
}

/**
 * Approves a drafted handoff summary, with the reviewer's edits.
 *
 * @param caseId - The case the summary belongs to.
 * @param summaryId - The draft being approved.
 * @param approvedText - Exactly the text the reviewer approves, which may differ from the draft.
 * @returns The approved summary. Fails with `forbidden` when the signed-in role may not approve and
 * `conflict` when the draft was already decided or replaced.
 *
 * @remarks
 * Side effect: posts to the approval endpoint. The API records `HandoffSummaryApproved` naming the
 * person signed in.
 */
export function approveHandoffSummary(
  caseId: string,
  summaryId: string,
  approvedText: string,
): Promise<ApiOutcome<HandoffSummary>> {
  return callApi(() =>
    api.POST('/cases/{caseId}/handoff-summary/{summaryId}/approval', {
      params: { path: { caseId, summaryId } },
      body: { approvedText },
    }),
  )
}

/**
 * Rejects a drafted handoff summary.
 *
 * @param caseId - The case the summary belongs to.
 * @param summaryId - The draft being rejected.
 * @param reason - Why the reviewer turned the draft down; kept with the rejection.
 * @returns The rejected summary. Fails with `forbidden` when the signed-in role may not reject and
 * `conflict` when the draft was already decided or replaced.
 *
 * @remarks
 * Side effect: posts to the rejection endpoint. Rejecting changes no stage, owner or deadline.
 */
export function rejectHandoffSummary(
  caseId: string,
  summaryId: string,
  reason: string,
): Promise<ApiOutcome<HandoffSummary>> {
  return callApi(() =>
    api.POST('/cases/{caseId}/handoff-summary/{summaryId}/rejection', {
      params: { path: { caseId, summaryId } },
      body: { reason },
    }),
  )
}
