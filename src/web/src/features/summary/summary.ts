import type { HandoffSummary, SummarySection } from '../../data'

/**
 * Heading of the summary section that AI never writes.
 *
 * @remarks
 * The assessment is the clinician's: AI never writes it (AI specification, sections 2.1 and 7), so
 * the screen never lets it be edited as part of the draft. Summary sections carry no kind, so the
 * section is recognised by its heading.
 */
export const ASSESSMENT_HEADING = 'Assessment'

/** Edited section texts, by the section's position in the summary. */
export type SectionEdits = Readonly<Record<number, string>>

/**
 * Tells whether a section is the assessment, which AI never writes and the reviewer does not edit here.
 *
 * @param section - A summary section.
 * @returns `true` when its heading is {@link ASSESSMENT_HEADING}, ignoring case and spaces around it.
 */
export function isAssessmentSection(section: Pick<SummarySection, 'heading'>): boolean {
  return section.heading.trim().toLowerCase() === ASSESSMENT_HEADING.toLowerCase()
}

/**
 * Applies a reviewer's edits to the drafted sections.
 *
 * @param sections - The sections as drafted.
 * @param edits - The edited texts, by section position.
 * @returns New sections with the edited texts; the assessment is never changed.
 */
export function applyEdits(sections: readonly SummarySection[], edits: SectionEdits): SummarySection[] {
  return sections.map((section, index) => {
    const edited = edits[index]
    return edited === undefined || isAssessmentSection(section) ? section : { ...section, text: edited }
  })
}

/**
 * Tells whether the reviewer changed any section.
 *
 * @param sections - The sections as drafted.
 * @param edits - The edited texts, by section position.
 * @returns `true` when at least one editable section differs from the draft.
 */
export function hasEdits(sections: readonly SummarySection[], edits: SectionEdits): boolean {
  return sections.some((section, index) => {
    const edited = edits[index]
    return edited !== undefined && !isAssessmentSection(section) && edited !== section.text
  })
}

/**
 * Runs sections together as plain text: each heading on its own line, followed by its text, with a
 * blank line between sections.
 *
 * @param sections - The sections, in reading order.
 * @returns The plain text a reviewer approves.
 *
 * @example
 * ```ts
 * composeSummaryText([{ heading: 'Situation', text: 'Needs assessment.' }]) // "Situation\nNeeds assessment."
 * ```
 */
export function composeSummaryText(sections: readonly Pick<SummarySection, 'heading' | 'text'>[]): string {
  return sections.map((section) => `${section.heading}\n${section.text.trim()}`).join('\n\n')
}

/**
 * Works out the exact text to approve.
 *
 * @param summary - The draft as the API returned it.
 * @param edits - The reviewer's edits.
 * @returns The draft's own text when nothing was changed, so what is approved is exactly what was
 * drafted; otherwise the edited sections run together with {@link composeSummaryText}.
 */
export function approvedTextFor(summary: HandoffSummary, edits: SectionEdits): string {
  return hasEdits(summary.sections, edits)
    ? composeSummaryText(applyEdits(summary.sections, edits))
    : summary.text
}
