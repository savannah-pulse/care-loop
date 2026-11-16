/**
 * Programmes (screen 4): each screening programme's settings from `GET /programmes`: steps with
 * deadline, reminder and owner role, the escalation ladder, evidence and closure rules, and the
 * change history. Read-only; placeholders and values waiting for clinical approval say so.
 */

export { ProgrammesPage } from './ProgrammesPage'
export type { ProgrammesPageProps } from './ProgrammesPage'
export {
  deadlineKey,
  evidenceKindName,
  isZeroDuration,
  ladderInOrder,
  programmeApproval,
  settingDuration,
} from './programmes'
export type { ProgrammeApproval } from './programmes'
