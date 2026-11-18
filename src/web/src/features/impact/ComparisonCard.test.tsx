import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { Read, ReadState, SimulationResult } from '../../data'
import { ComparisonCard } from './ImpactPage'

function simulation(state: ReadState<SimulationResult>, retry: () => void = () => undefined): Read<SimulationResult> {
  return { state, retry }
}

const LOADING = simulation({ status: 'loading' })

describe('the comparison on Impact', () => {
  it('stays off the page while the comparison runs, and while the API says it is not built yet (501)', () => {
    const { container, rerender } = render(<ComparisonCard read={LOADING} />)
    expect(container).toBeEmptyDOMElement()

    rerender(
      <ComparisonCard
        read={simulation({ status: 'failed', failure: { kind: 'notAvailable', status: 501 }, failedAt: new Date() })}
      />,
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('says calmly when the comparison is not available right now, and keeps the card while checking again', async () => {
    const retry = vi.fn()
    const failed = simulation({ status: 'failed', failure: { kind: 'error' }, failedAt: new Date() }, retry)
    const { rerender } = render(<ComparisonCard read={failed} />)
    expect(screen.getByRole('heading', { name: 'What reminders and escalation change' })).toBeInTheDocument()
    expect(screen.getByText("The comparison isn't available right now")).toBeInTheDocument()

    await userEvent.setup().click(screen.getByRole('button', { name: 'Check again' }))
    expect(retry).toHaveBeenCalledOnce()
    rerender(<ComparisonCard read={simulation({ status: 'loading' }, retry)} />)
    expect(screen.getByRole('heading', { name: 'What reminders and escalation change' })).toBeInTheDocument()
    expect(screen.getByText('Running the comparison')).toBeInTheDocument()
  })
})
