/**
 * Handoff summary (screen 18): a clinician asks AI for a draft, checks each section against the facts
 * it cites and the gaps it reports, edits it if needed, and approves or rejects it. AI output stays a
 * draft until a person approves it. An open draft and its edits are kept in memory, never in browser
 * storage, while the reviewer looks at other screens.
 */

export { SummaryPage } from './SummaryPage'
export type { SummaryPageProps } from './SummaryPage'
export { forgetOpenDraft, forgetOpenDrafts, keepOpenDraft, openDraftFor } from './drafts'
export type { OpenDraft } from './drafts'
export {
  ASSESSMENT_HEADING,
  applyEdits,
  approvedTextFor,
  composeSummaryText,
  hasEdits,
  isAssessmentSection,
} from './summary'
export type { SectionEdits } from './summary'
