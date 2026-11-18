import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { STAFF } from './identity'
import { answer, json, sentRequests } from './testing/api'
import { caseDetail, caseSummary, escalation, programme } from './testing/builders'
import { renderApp } from './testing/screens'

describe('who is acting', () => {
  it.each(STAFF.map((person) => [person.name, person] as const))(
    'tells the API on every request that %s is acting, in their role and team',
    async (_name, person) => {
      const detail = caseDetail({
        facilityId: person.facilityId ?? 'family-health-centre',
        isEscalated: true,
        exceptions: ['Overdue'],
        escalations: [escalation()],
      })
      answer(`GET /cases/${detail.caseId}`, json(detail))
      answer(`GET /cases/${detail.caseId}/timeline`, json([]))
      answer('GET /programmes/breast-screening', json(programme()))
      answer('GET /cases', json([caseSummary({ ...detail })]))
      renderApp({ at: `#/cases/${detail.caseId}`, as: person.id })

      await waitFor(() =>
        expect(within(screen.getByRole('list', { name: 'When a step is late' })).getAllByRole('listitem')).toHaveLength(3),
      )
      const requests = sentRequests()
      expect(requests.map((request) => request.route)).toEqual(
        expect.arrayContaining([
          `GET /cases/${detail.caseId}`,
          `GET /cases/${detail.caseId}/timeline`,
          'GET /programmes/breast-screening',
        ]),
      )
      for (const request of requests) {
        expect(request.headers.get('X-Demo-User')).toBe(person.reference)
        expect(request.headers.get('X-Demo-Role')).toBe(person.role)
        expect(request.headers.get('X-Demo-Facility')).toBe(person.facilityId ?? null)
      }
    },
  )

  it('sends the next person’s identity as soon as they sign in, with no facility for the programme coordinator', async () => {
    // A call-back, so the case is on the nurse navigator's own list, which the worklist opens on.
    answer('GET /cases', json([caseSummary({ isEscalated: true, exceptions: ['Overdue'], nextAction: 'call-patient' })]))
    const { user } = renderApp({ at: '#/', as: 'huda-saleh' })
    await screen.findByRole('link', { name: 'Hessa Al Dhaheri, MRN 100\u00a0245\u00a0891' })
    const before = sentRequests().length

    await user.click(screen.getByRole('button', { name: (label) => label.startsWith('Signed in as') }))
    await user.click(screen.getByRole('button', { name: (label) => label.startsWith('Dr. Obaid Al Suwaidi') }))

    expect(await screen.findByRole('link', { name: 'Case 3F6B1C2A' })).toBeInTheDocument()
    const after = sentRequests().slice(before)
    expect(after.length).toBeGreaterThan(0)
    for (const request of after) {
      expect(request.headers.get('X-Demo-User')).toBe('obaid-al-suwaidi')
      expect(request.headers.get('X-Demo-Role')).toBe('coordinator')
      expect(request.headers.has('X-Demo-Facility')).toBe(false)
    }
  })
})
