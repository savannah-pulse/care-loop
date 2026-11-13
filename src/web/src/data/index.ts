/**
 * Data access: the typed CareLoop API client, the identity headers it sends, and small hooks that
 * follow each read through loading, data, "not available yet" (HTTP 501) and failure.
 *
 * @remarks
 * The client is generated from the API's OpenAPI document (`src/api/schema.d.ts`). Nothing here
 * knows or cares what system answers.
 */

export { API_BASE_URL, ROLE_HEADER, USER_HEADER, api, identityHeaders } from './client'
export { callApi, failureKindFor } from './outcome'
export type { ApiFailure, ApiOutcome, FailureKind } from './outcome'
export { useApiRead } from './read'
export type { Loader, Read, ReadState } from './read'
export {
  CASE_LIST_LIMIT,
  COMPARISON_COHORT,
  useCase,
  useCaseTimeline,
  useCases,
  useMetrics,
  useSimulation,
} from './reads'
export { approveHandoffSummary, draftHandoffSummary, rejectHandoffSummary } from './summaries'
export type * from './types'
