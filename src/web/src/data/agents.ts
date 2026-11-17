import { useCallback } from 'react'
import { api, type CareLoopClient } from './client'
import { callApi, type ApiOutcome } from './outcome'
import { useApiRead, type Read } from './read'
import { reasonParameter } from './reads'
import type { AiText, MessageChannel } from './types'

/**
 * Asks the role-briefing agent for a few words on where the signed-in person should start today.
 *
 * @param signal - Cancels the call.
 * @param client - The client to call with; the dashboard's own by default.
 * @returns The agent's text. Fails with `notAvailable` while AI is switched off (HTTP 501) and
 * `unavailable` when the agent could not write it (HTTP 503).
 *
 * @remarks
 * Side effect: posts to `POST /ai/role-briefing` for the reader's own team. The API builds the
 * prompt from the worklist's numbers and case numbers, never from names, and records the read.
 */
export function draftRoleBriefing(signal?: AbortSignal, client: CareLoopClient = api): Promise<ApiOutcome<AiText>> {
  return callApi(() => client.POST('/ai/role-briefing', { signal }))
}

/**
 * Asks the impact-summary agent what the numbers on the Impact screen say.
 *
 * @param signal - Cancels the call.
 * @param client - The client to call with; the dashboard's own by default.
 * @returns The agent's text. Fails with `notAvailable` while AI is switched off (HTTP 501) and
 * `unavailable` when the agent could not write it (HTTP 503).
 *
 * @remarks
 * Side effect: posts to `POST /ai/impact-summary`, for the same team as `GET /metrics`. The API
 * builds the prompt from the totals only, never from a case, and records the read.
 */
export function draftImpactSummary(signal?: AbortSignal, client: CareLoopClient = api): Promise<ApiOutcome<AiText>> {
  return callApi(() => client.POST('/ai/impact-summary', { signal }))
}

/** What {@link draftPatientMessage} asks for. */
export interface PatientMessageQuery {
  /** The case the message would be about. */
  readonly caseId: string
  /** The channel to draft for: a text message or an email. */
  readonly channel: MessageChannel
  /** Why the reader opened a case outside their team, when they gave one. */
  readonly reason?: string
}

/**
 * Asks the patient-notification agent for a preview of a message to a case's patient. Nothing is
 * sent.
 *
 * @param query - The case, the channel and any reason given for a case outside the reader's team.
 * @param signal - Cancels the call.
 * @param client - The client to call with; the dashboard's own by default.
 * @returns The draft. Fails with `forbidden` for a role other than nurse navigator or clinician,
 * `notAvailable` while AI is switched off (HTTP 501) and `unavailable` when the agent could not
 * write it (HTTP 503).
 *
 * @remarks
 * Side effect: posts to `POST /cases/{caseId}/patient-message-draft`, which records the read like
 * a case read. The preview is never sent or stored: patient messages go out from approved
 * templates.
 */
export function draftPatientMessage(
  query: PatientMessageQuery,
  signal?: AbortSignal,
  client: CareLoopClient = api,
): Promise<ApiOutcome<AiText>> {
  return callApi(() =>
    client.POST('/cases/{caseId}/patient-message-draft', {
      params: {
        path: { caseId: query.caseId },
        query: { channel: query.channel, reason: reasonParameter(query.reason) },
      },
      signal,
    }),
  )
}

/**
 * Reads the role briefing for the signed-in person.
 *
 * @returns The read of `POST /ai/role-briefing`.
 *
 * @remarks
 * Side effects: posts to the API on mount, on `retry` and when someone else signs in. Mount the
 * component that uses it only when there is something to sum up, so no agent is asked for nothing.
 */
export function useRoleBriefing(): Read<AiText> {
  const load = useCallback((signal: AbortSignal) => draftRoleBriefing(signal), [])
  return useApiRead(load)
}

/**
 * Reads what the impact-summary agent says about the signed-in person's numbers.
 *
 * @returns The read of `POST /ai/impact-summary`.
 *
 * @remarks
 * Side effects: posts to the API on mount, on `retry` and when someone else signs in. It runs
 * beside `GET /metrics`, so the numbers never wait for AI.
 */
export function useImpactSummary(): Read<AiText> {
  const load = useCallback((signal: AbortSignal) => draftImpactSummary(signal), [])
  return useApiRead(load)
}

/**
 * Reads a preview of a message to a case's patient.
 *
 * @param query - The case, the channel and any reason given for a case outside the reader's team.
 * @returns The read of `POST /cases/{caseId}/patient-message-draft`.
 *
 * @remarks
 * Side effects: posts to the API on mount, when the case, channel or reason changes, on `retry`
 * and when someone else signs in. Nothing is sent to the patient.
 */
export function usePatientMessageDraft({ caseId, channel, reason }: PatientMessageQuery): Read<AiText> {
  const load = useCallback(
    (signal: AbortSignal) => draftPatientMessage({ caseId, channel, reason }, signal),
    [caseId, channel, reason],
  )
  return useApiRead(load)
}
