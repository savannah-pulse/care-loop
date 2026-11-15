import { describe, expect, it } from 'vitest'
import type { HandoffSummary } from '../../data'
import { approvedTextFor, hasEdits } from './summary'

function draft(): HandoffSummary {
  return {
    summaryId: 'draft-1',
    caseId: 'case-1',
    status: 'Draft',
    sections: [
      { heading: 'Situation', text: 'Needs further assessment.', sourceFactIds: ['screening-report-2026-11-10'] },
      { heading: 'Assessment', text: 'For the clinician to write.', sourceFactIds: [] },
      { heading: 'Recommendation', text: 'Book the assessment clinic.', sourceFactIds: ['referral-declined-2026-11-10'] },
    ],
    text: 'Situation\nNeeds further assessment.\n\nAssessment\nFor the clinician to write.\n\nRecommendation\nBook the assessment clinic.\n',
    missingOrConflictingFacts: [],
    humanReviewRequired: true,
    draftedBy: 'careloop-ai',
    modelDeployment: 'synthetic-model',
    promptVersion: 'handoff-v1',
    trigger: 'manual',
    language: 'en',
    draftedAt: '2026-11-12T05:00:00Z',
  }
}

describe('handoff summary approval', () => {
  it('approves the draft exactly as drafted when nothing was changed', () => {
    const summary = draft()
    expect(approvedTextFor(summary, {})).toBe(summary.text)
    expect(approvedTextFor(summary, { 0: 'Needs further assessment.' })).toBe(summary.text)
  })

  it('approves exactly the edited text, spaces and line breaks included', () => {
    const edited = '  Needs further assessment.\n\nHer son has the appointment details.  '
    expect(approvedTextFor(draft(), { 0: edited })).toBe(
      `Situation\n${edited}\n\nAssessment\nFor the clinician to write.\n\nRecommendation\nBook the assessment clinic.`,
    )
  })

  it('never lets the assessment, which is the clinician’s, be changed as part of the draft', () => {
    expect(hasEdits(draft().sections, { 1: 'Written by someone else' })).toBe(false)
    expect(approvedTextFor(draft(), { 1: 'Written by someone else', 2: 'Refer to the General Hospital.' })).toContain(
      'Assessment\nFor the clinician to write.',
    )
  })
})
