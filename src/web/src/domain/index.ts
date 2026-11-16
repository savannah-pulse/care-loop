/**
 * Display rules: how the API's coordination data becomes plain words on screen. The ten display
 * stages, status words and their colours, next actions and why they are needed, tracks,
 * exceptions, urgency, programme names, patients, people, escalations and timeline entries.
 *
 * @remarks
 * These rules only describe what the API reports. They never decide anything clinical.
 */

export {
  canAcknowledge,
  currentEscalation,
  deadlineFor,
  escalationLadder,
  escalationNotices,
  escalationTarget,
  isAddressedTo,
  noticeActor,
  openEscalations,
} from './escalations'
export type { EscalationTarget, LadderInput, LadderRung, Notice, NoticeKind, RungState } from './escalations'
export { EXCEPTION_NAMES, exceptionLabel, exceptionReason, isExceptionName } from './exceptions'
export type { ExceptionName } from './exceptions'
export { NEXT_ACTION_KEYS, REFERRAL_ACTIONS, isNextActionKey, isReferralAction, nextActionText } from './nextActions'
export type { NextActionKey } from './nextActions'
export {
  caseLinkNames,
  caseNumber,
  describePatient,
  outcomeEvidenceText,
  resultSourceText,
  secondaryIdentifierText,
} from './patients'
export type { CaseLinkSubject, PatientFields, PatientView } from './patients'
export { describeActor, describeOwner } from './people'
export type { PersonDetail, PersonView } from './people'
export { programmeName } from './programmes'
export { nextActionReason, nextActionWhy } from './reasons'
export { DISPLAY_LABELS, displayLabelText, displayStepIndex, isDisplayLabel } from './stages'
export { CASE_STATUS_KINDS, caseStatus, needsAttention } from './status'
export type { CaseStatus, CaseStatusInput, CaseStatusKind } from './status'
export {
  GENERIC_REASONS,
  SOURCE_SYSTEMS,
  groupByDay,
  hasOwnReason,
  mayQuoteReason,
  newestFirst,
  refusalReason,
  sourceLabel,
  timelineEntryTitle,
  timelineKind,
  timelineSource,
  timelineTitle,
} from './timeline'
export type { TimelineDay, TimelineSource } from './timeline'
export type { Tone } from './tone'
export { TRACK_KINDS, describeTrack } from './tracks'
export type { TrackView } from './tracks'
export { URGENCY_LEVELS, isUrgent, urgencyLevel, urgencyText } from './urgency'
export type { UrgencyLevel } from './urgency'
