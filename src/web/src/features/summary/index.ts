/**
 * Handoff summary (screen 18): a clinician asks AI for a draft, checks each section against the facts
 * it cites and the gaps it reports, edits it if needed, and approves or rejects it. AI output stays a
 * draft until a person approves it.
 */

export { SummaryPage } from './SummaryPage'
export type { SummaryPageProps } from './SummaryPage'
export {
  ASSESSMENT_HEADING,
  applyEdits,
  approvedTextFor,
  composeSummaryText,
  hasEdits,
  isAssessmentSection,
} from './summary'
export type { SectionEdits } from './summary'
