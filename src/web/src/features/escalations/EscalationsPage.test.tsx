import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { answer, json } from '../../testing/api'
import { caseDetail, caseSummary, escalation } from '../../testing/builders'
import { renderApp } from '../../testing/screens'

const MARIAM = caseSummary({
  caseId: '4d5e6f7a-4444-4444-8444-100245894004',
  patientReference: 'p-100245894',
  patientDisplayName: 'Mariam Al Mansoori',
  patientSecondaryIdentifier: '100245894',
  isEscalated: true,
  exceptions: ['Overdue'],
})
const NAMESAKE = caseSummary({
  caseId: '6f7a8b9c-6666-4666-8666-100245896006',
  patientReference: 'p-100245896',
  patientDisplayName: 'Mariam Al Mansoori',
  patientSecondaryIdentifier: '100245896',
  isEscalated: true,
  exceptions: ['Overdue'],
})

describe('escalations', () => {
  it('names each escalated case by its patient and MRN, so namesakes never share a link name', async () => {
    answer('GET /cases', json([MARIAM, NAMESAKE]))
    for (const item of [MARIAM, NAMESAKE]) {
      answer(`GET /cases/${item.caseId}`, json(caseDetail({ ...item, escalations: [escalation()] })))
    }
    renderApp({ at: '#/escalations', as: 'joy-bautista' })

    for (const [name, caseId] of [
      ['Mariam Al Mansoori, MRN 100\u00a0245\u00a0894', MARIAM.caseId],
      ['Mariam Al Mansoori, MRN 100\u00a0245\u00a0896', NAMESAKE.caseId],
    ]) {
      const link = await screen.findByRole('link', { name })
      expect(link).toHaveAttribute('href', `#/cases/${caseId}/escalation`)
      expect(link).toHaveTextContent(/^Mariam Al Mansoori$/)
    }
  })
})
