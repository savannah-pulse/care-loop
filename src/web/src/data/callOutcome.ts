import { api, type CareLoopClient } from './client'
import { callApi, type ApiOutcome } from './outcome'
import { reasonParameter } from './reads'
import type { CallOutcome, CaseDetail } from './types'

/**
 * Records what a nurse navigator learned from the patient on a call.
 *
 * @param caseId - The case the call was about.
 * @param outcome - What the patient said: that they will come, that they said the next step back,
 * and how confident they feel from 1 to 5. At least one must be recorded.
 * @param reason - The reason given for a case outside the person's team, when there is one.
 * @param client - The client to call with; the dashboard's own by default.
 * @returns The case as it now stands. Fails with `forbidden` when the person is not a nurse
 * navigator, or the case is outside their team without a reason; with `invalid` when nothing was
 * recorded or the rating is not from 1 to 5; with `conflict` when a rule refused it, for example
 * because no message was ever sent to the patient.
 *
 * @remarks
 * Side effect: posts to `POST /cases/{caseId}/call-outcome`. Patients are reached by text message
 * and email only, and a reply there can only say "I will come" or ask for a call, so understanding
 * and confidence are recorded here, by a person. Recording never changes who is responsible.
 */
export function recordCallOutcome(
  caseId: string,
  outcome: CallOutcome,
  reason?: string,
  client: CareLoopClient = api,
): Promise<ApiOutcome<CaseDetail>> {
  return callApi(() =>
    client.POST('/cases/{caseId}/call-outcome', {
      params: { path: { caseId }, query: { reason: reasonParameter(reason) } },
      body: outcome,
    }),
  )
}
