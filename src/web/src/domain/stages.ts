import { t, type MessageKey } from '../i18n'
import type { CaseStage, TrackStatus } from '../data'

/** The five pathway stages in the API, in the order a case moves through them. */
export const CASE_STAGES: readonly CaseStage[] = [
  'Received',
  'ReviewPending',
  'FollowUpActive',
  'ClosureReview',
  'Closed',
]

/**
 * The ten stages shown on screen, in order: catalogue keys of their labels.
 *
 * @remarks
 * Every case, in every programme, moves through the same ten display stages. They are labels over
 * the API's five stages and four tracks; {@link currentStepIndex} maps one to the other.
 */
export const DISPLAY_STEPS: readonly MessageKey[] = [
  'stages.steps.resultReceived',
  'stages.steps.caseOpened',
  'stages.steps.ownerAssigned',
  'stages.steps.patientNotified',
  'stages.steps.patientAcknowledged',
  'stages.steps.consultation',
  'stages.steps.followUp',
  'stages.steps.outcomeRecorded',
  'stages.steps.closureReview',
  'stages.steps.closed',
]

const ACKNOWLEDGED_STATUSES: readonly string[] = ['Acknowledged', 'Understood']

function trackStatus(tracks: readonly TrackStatus[], kind: TrackStatus['kind']): string | undefined {
  return tracks.find((track) => track.kind === kind)?.status ?? undefined
}

/**
 * Works out which of the ten display stages a case is at (rule C9 of the UI design).
 *
 * @param stage - The case's stage in the API.
 * @param tracks - The state of its four tracks, from `GET /cases/{caseId}`.
 * @returns The zero-based index into {@link DISPLAY_STEPS}.
 *
 * @remarks
 * Labels 1 to 3 and 8 to 10 follow the stage. In `FollowUpActive` the tracks run side by side, so
 * the label shows the furthest step reached: Follow-up once a referral or appointment exists,
 * otherwise Consultation once one is requested, otherwise Patient acknowledged once the
 * notification is acknowledged or understood, otherwise Patient notified.
 *
 * @example
 * ```ts
 * currentStepIndex('FollowUpActive', [{ kind: 'Referral', status: 'Rejected', isComplete: false }]) // 6, "Follow-up"
 * ```
 */
export function currentStepIndex(stage: CaseStage, tracks: readonly TrackStatus[]): number {
  switch (stage) {
    case 'Received':
      return 1
    case 'ReviewPending':
      return 2
    case 'FollowUpActive':
      if (trackStatus(tracks, 'Referral') || trackStatus(tracks, 'Appointment')) {
        return 6
      }
      if (trackStatus(tracks, 'Consultation')) {
        return 5
      }
      return ACKNOWLEDGED_STATUSES.includes(trackStatus(tracks, 'Notification') ?? '') ? 4 : 3
    case 'ClosureReview':
      return 8
    case 'Closed':
      return 9
  }
}

/**
 * Names a stage for the worklist, where the tracks are not known.
 *
 * @param stage - The case's stage in the API.
 * @returns `Case opened`, `Owner assigned`, `In follow-up`, `Closure review` or `Closed`.
 *
 * @remarks
 * `GET /cases` does not return track statuses, so the worklist cannot apply rule C9 and shows one
 * label per API stage. The case screen shows the exact display stage.
 */
export function stageLabel(stage: CaseStage): string {
  return t(`stages.list.${stage}`)
}

/**
 * Says where a stage sits among the ten display stages, for the worklist.
 *
 * @param stage - The case's stage in the API.
 * @returns For example `Stage 3 of 10`, or `Stages 4 to 7 of 10` during follow-up.
 */
export function stagePosition(stage: CaseStage): string {
  switch (stage) {
    case 'Received':
      return t('stages.position', { number: 2 })
    case 'ReviewPending':
      return t('stages.position', { number: 3 })
    case 'FollowUpActive':
      return t('stages.followUpPosition')
    case 'ClosureReview':
      return t('stages.position', { number: 9 })
    case 'Closed':
      return t('stages.position', { number: 10 })
  }
}
