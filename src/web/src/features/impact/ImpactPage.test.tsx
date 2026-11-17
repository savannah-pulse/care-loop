import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { answer, json, problem, sentRequests } from '../../testing/api'
import { aiText, metrics } from '../../testing/builders'
import { renderApp } from '../../testing/screens'

const SUMMARY = 'POST /ai/impact-summary'
const CAPTION = 'Written by AI from the numbers on this page. Check them before you act on it.'

function openImpact(words: Parameters<typeof answer>[1], as = 'joy-bautista'): void {
  answer('GET /metrics', json(metrics()))
  answer('POST /metrics/simulation', problem(501, 'RunSimulation is scaffolded but not implemented yet (WS10).'))
  // The header counts the escalations waiting for the facility lead and the programme coordinator.
  answer('GET /cases', json([]))
  answer(SUMMARY, words)
  renderApp({ at: '#/impact', as })
}

async function card(): Promise<HTMLElement> {
  return screen.findByRole('region', { name: 'What the numbers say' })
}

describe('what the numbers say', () => {
  it('shows what the impact-summary agent wrote, captioned as AI, asked for with the person’s identity', async () => {
    openImpact(
      json(
        aiText({
          agent: 'impact-summary',
          text: 'Most next steps are done by their deadline.\n\nWhere cases wait, it is mostly for a unit to accept a referral.',
        }),
      ),
    )

    const region = await card()
    const words = await within(region).findByRole('figure', { name: CAPTION })
    expect(within(words).getAllByText(/./, { selector: 'p' }).map((paragraph) => paragraph.textContent)).toEqual([
      'Most next steps are done by their deadline.',
      'Where cases wait, it is mostly for a unit to accept a referral.',
    ])
    expect(within(region).getByRole('heading', { level: 2, name: 'What the numbers say' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Next care step done by the deadline' })).toBeInTheDocument()
    const [request, ...more] = sentRequests(SUMMARY)
    expect(more).toHaveLength(0)
    expect(request.headers.get('X-Demo-User')).toBe('joy-bautista')
    expect(request.headers.get('X-Demo-Role')).toBe('facility-lead')
    expect(request.headers.get('X-Demo-Facility')).toBe('family-health-centre')
  })

  it('shows a skeleton while AI writes, and the numbers without waiting for it', async () => {
    let release: (response: Response) => void = () => undefined
    openImpact(() => new Promise<Response>((resolve) => (release = resolve)))

    const region = await card()
    expect(within(region).getByRole('status')).toHaveTextContent('Loading what the numbers say')
    expect(screen.getByText('85%')).toBeInTheDocument()
    release(json(aiText({ agent: 'impact-summary' })))
    expect(await within(region).findByRole('figure', { name: CAPTION })).toBeInTheDocument()
  })

  it('says in one calm line that it is not ready yet while AI is switched off (501)', async () => {
    openImpact(problem(501, 'ImpactSummary is switched off here.'))

    const region = await card()
    expect(await within(region).findByText('Not ready yet. This will show here once it is switched on.')).toBeInTheDocument()
    expect(within(region).getByText('Every number on this page is up to date.')).toBeInTheDocument()
    expect(within(region).queryByRole('figure')).not.toBeInTheDocument()
  })

  it('says calmly that AI could not write it (503), and every number stays', async () => {
    openImpact(problem(503, 'AI could not write this'))

    const region = await card()
    expect(
      await within(region).findByText('AI could not write it this time. Every number on this page is still up to date.'),
    ).toBeInTheDocument()
    expect(within(region).queryByRole('figure')).not.toBeInTheDocument()
    expect(screen.getByText('85%')).toBeInTheDocument()
  })

  it('asks for every facility’s numbers for the programme coordinator, with no facility header', async () => {
    openImpact(json(aiText({ agent: 'impact-summary' })), 'obaid-al-suwaidi')

    await within(await card()).findByRole('figure', { name: CAPTION })
    expect(sentRequests(SUMMARY)[0].headers.has('X-Demo-Facility')).toBe(false)
    expect(sentRequests(SUMMARY)[0].headers.get('X-Demo-Role')).toBe('coordinator')
  })
})
