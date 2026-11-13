/**
 * Display rules: how the API's coordination data becomes plain words on screen. The ten display
 * stages (rule C9), status words and their colours, next actions and why they are needed, tracks,
 * exceptions, urgency, programme names, people and timeline entries.
 *
 * @remarks
 * These rules only describe what the API reports. They never decide anything clinical.
 */

export { EXCEPTION_NAMES, exceptionLabel, exceptionReason, isExceptionName } from './exceptions'
export type { ExceptionName } from './exceptions'
export { NEXT_ACTION_KEYS, REFERRAL_ACTIONS, isNextActionKey, isReferralAction, nextActionText } from './nextActions'
export type { NextActionKey } from './nextActions'
export { describeActor, describeOwner } from './people'
export type { PersonDetail, PersonView } from './people'
export { programmeName } from './programmes'
export { nextActionReason } from './reasons'
export { CASE_STAGES, DISPLAY_STEPS, currentStepIndex, stageLabel } from './stages'
export { CASE_STATUS_KINDS, caseStatus, needsAttention } from './status'
export type { CaseStatus, CaseStatusInput, CaseStatusKind } from './status'
export { groupByDay, newestFirst, timelineKind, timelineSource, timelineTitle } from './timeline'
export type { TimelineDay, TimelineSource } from './timeline'
export type { Tone } from './tone'
export { TRACK_KINDS, describeTrack } from './tracks'
export type { TrackView } from './tracks'
export { URGENCY_LEVELS, isUrgent, urgencyLevel, urgencyText } from './urgency'
export type { UrgencyLevel } from './urgency'
