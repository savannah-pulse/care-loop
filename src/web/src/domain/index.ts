/**
 * Display rules: how the API's coordination data becomes plain words on screen. The ten display
 * stages (rule C9), status words and their colours, next actions, tracks, exceptions, urgency,
 * programme names, owners and timeline titles.
 *
 * @remarks
 * These rules only describe what the API reports. They never decide anything clinical.
 */

export { EXCEPTION_NAMES, exceptionLabel, exceptionReason, isExceptionName } from './exceptions'
export type { ExceptionName } from './exceptions'
export { NEXT_ACTION_KEYS, isNextActionKey, nextActionText } from './nextActions'
export type { NextActionKey } from './nextActions'
export { describeActor, describeOwner } from './people'
export type { PersonDetail, PersonView } from './people'
export { programmeName } from './programmes'
export { CASE_STAGES, DISPLAY_STEPS, currentStepIndex, stageLabel, stagePosition } from './stages'
export { CASE_STATUS_KINDS, caseStatus, needsAttention } from './status'
export type { CaseStatus, CaseStatusInput, CaseStatusKind } from './status'
export { newestFirst, timelineTitle } from './timeline'
export type { Tone } from './tone'
export { TRACK_KINDS, describeTrack } from './tracks'
export type { TrackView } from './tracks'
export { URGENCY_LEVELS, isUrgent, urgencyLevel, urgencyText } from './urgency'
export type { UrgencyLevel } from './urgency'
