/**
 * Data access: the typed CareLoop API client, the identity headers it sends, and small hooks that
 * follow each read through loading, data, "not available yet" (HTTP 501) and failure.
 *
 * @remarks
 * The client is generated from the API's OpenAPI document (`src/api/schema.d.ts`). Nothing here
 * knows or cares what system answers.
 */

export {
  API_BASE_URL,
  FACILITY_HEADER,
  ROLE_HEADER,
  USER_HEADER,
  api,
  createCareLoopClient,
  identityHeaders,
} from './client'
export type { CareLoopClient, CareLoopClientOptions } from './client'
export {
  draftImpactSummary,
  draftPatientMessage,
  draftRoleBriefing,
  useImpactSummary,
  usePatientMessageDraft,
  useRoleBriefing,
} from './agents'
export type { PatientMessageQuery } from './agents'
export { recordCallOutcome } from './callOutcome'
export {
  DETAIL_READS_AT_ONCE,
  acknowledgeEscalation,
  escalationsChanged,
  loadEscalatedCases,
  mapAtMost,
  useEscalatedCases,
  useEscalationCount,
  useEscalationsVersion,
} from './escalations'
export type { EscalatedCase } from './escalations'
export { callApi, failureKindFor } from './outcome'
export type { ApiFailure, ApiOutcome, FailureKind } from './outcome'
export { useApiRead } from './read'
export type { Loader, Read, ReadState } from './read'
export {
  CASE_LIST_LIMIT,
  COMPARISON_COHORT,
  PAGE_SIZE,
  loadCaseList,
  readCase,
  reasonParameter,
  useCase,
  useCaseList,
  useCaseTimeline,
  useMetrics,
  usePatientCases,
  useProgramme,
  useProgrammes,
  useSimulation,
} from './reads'
export type { CaseList, CaseListQuery, LoadCaseListOptions } from './reads'
export { approveHandoffSummary, draftHandoffSummary, rejectHandoffSummary } from './summaries'
export type * from './types'
