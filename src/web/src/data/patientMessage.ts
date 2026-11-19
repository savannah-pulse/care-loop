import { api, type CareLoopClient } from './client'
import { callApi, type ApiOutcome } from './outcome'
import { reasonParameter } from './reads'
import type { AiText, MessageChannel } from './types'

/**
 * Asks AI for a draft of a message to the patient of one case. Nothing is sent or stored.
 *
 * @param caseId - The case.
 * @param channel - Text message or email; the draft is written to that channel's length.
 * @param reason - Why the reader opened a case outside their team, if they did.
 * @param client - The client to call with; the dashboard's own by default.
 * @returns The draft, or why there is none: `conflict` while AI is switched off, `forbidden` for a
 * role that may not ask, `unavailable` when AI could not write it.
 *
 * @remarks
 * Side effect: calls `GET /cases/{caseId}/patient-message-draft`. The API never reuses a draft, so
 * every call asks the AI provider again.
 */
export function draftPatientMessage(
  caseId: string,
  channel: MessageChannel,
  reason?: string,
  client: CareLoopClient = api,
): Promise<ApiOutcome<AiText>> {
  return callApi(() =>
    client.GET('/cases/{caseId}/patient-message-draft', {
      params: { path: { caseId }, query: { channel, reason: reasonParameter(reason) } },
    }),
  )
}
