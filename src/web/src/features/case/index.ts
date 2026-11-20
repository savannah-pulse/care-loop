/**
 * Case (screen 2): everything about one case on one page. The patient's name and MRN, the ten
 * display stages, the single next action with its owner, due time and escalation ladder, the four
 * tracks, the timeline with each entry's source and, for clinicians, the patient's other screening.
 * A nurse navigator records what the patient said on a call. A nurse navigator or clinician can
 * ask AI for a draft of a message to the patient, which is never sent from here. A case at another facility asks why
 * first.
 */

export { CallOutcomeCard } from './CallOutcomeCard'
export type { CallOutcomeCardProps } from './CallOutcomeCard'
export {
  CONFIDENCE_RATINGS,
  EMPTY_CALL_OUTCOME,
  MAX_CONFIDENCE,
  MIN_CONFIDENCE,
  callOutcomeOffer,
  canRecord,
  recordedConfidence,
  toCallOutcome,
} from './callOutcome'
export type { CallOutcomeDraft, CallOutcomeOffer } from './callOutcome'
export { CasePage } from './CasePage'
export type { CasePageProps } from './CasePage'
export { NextActionCard } from './NextActionCard'
export type { NextActionCardProps } from './NextActionCard'
export { OtherScreeningCard } from './OtherScreeningCard'
export type { OtherScreeningCardProps } from './OtherScreeningCard'
export { PatientMessageCard } from './PatientMessageCard'
export type { PatientMessageCardProps } from './PatientMessageCard'
export { StageStepper } from './StageStepper'
export type { StageStepperProps } from './StageStepper'
export { TIMELINE_PAGE, TimelineCard } from './TimelineCard'
export type { TimelineCardProps } from './TimelineCard'
export { TracksCard } from './TracksCard'
export type { TracksCardProps } from './TracksCard'
