import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { signInAs } from '../../identity'
import { answer, json, problem, sentRequests } from '../../testing/api'
import { ImpactSummary } from './ImpactSummary'

describe('impact summary', () => {
  beforeEach(() => {
    signInAs('mona-khalil')
  })

  it('shows the AI reading of the figures with its paragraphs, labelled as written by AI, and asks for a new one on Refresh', async () => {
    answer('GET /ai/impact-summary', (request) =>
      json({
        text: request.query.get('refresh') === 'true' ? 'SYN new reading.' : 'SYN 9 of 28 open cases are past their deadline.\n\nSYN limits note.',
        model: 'Foundry/impact-summary',
        generatedAt: new Date().toISOString(),
        cached: false,
      }),
    )
    render(<ImpactSummary />)

    const summary = await screen.findByRole('region', { name: 'Summary of the figures, written by AI' })
    expect(within(summary).getByText(/SYN 9 of 28 open cases are past their deadline\.\s+SYN limits note\./)).toBeInTheDocument()
    expect(within(summary).getByText(/^Written by AI · Today, /)).toBeInTheDocument()
    expect(sentRequests('GET /ai/impact-summary')[0].query.has('refresh')).toBe(false)

    await userEvent.click(within(summary).getByRole('button', { name: 'Refresh' }))

    expect(await screen.findByText('SYN new reading.')).toBeInTheDocument()
    expect(sentRequests('GET /ai/impact-summary').at(-1)?.query.get('refresh')).toBe('true')
  })

  it.each([
    ['AI is switched off', 409],
    ['AI could not write it', 503],
  ])('shows nothing when %s', async (_why, status) => {
    answer('GET /ai/impact-summary', problem(status))
    const { container } = render(<ImpactSummary />)

    expect(await screen.findByRole('status')).toHaveTextContent('Writing a summary of these figures…')
    await expect.poll(() => container.textContent).toBe('')
  })
})
