import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { AiText, Read } from '../data'
import { AiSummary } from './AiSummary'

const now = new Date()

const read: Read<AiText> = {
  state: {
    status: 'ready',
    data: { text: 'SYN summary.', model: 'Foundry/impact-summary', generatedAt: now.toISOString(), cached: false },
  },
  retry: () => undefined,
}

function show() {
  return render(
    <AiSummary read={read} now={now} label="Summary" loadingLabel="Writing…" refreshing={false} onRefresh={() => undefined} />,
  )
}

describe('AI summary', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('shows a text that fits whole, with no button to show more', () => {
    show()

    expect(screen.getByText('SYN summary.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument()
  })

  it('cuts a text that does not fit, and shows the rest when asked', async () => {
    // The test browser lays nothing out, so say the text is taller than its three lines.
    vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(400)
    vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(72)
    show()

    const more = screen.getByRole('button', { name: 'Show more' })
    expect(more).toHaveAttribute('aria-expanded', 'false')
    expect(screen.getByText('SYN summary.')).toHaveClass('ai-brief-clamped')

    await userEvent.click(more)

    expect(screen.getByText('SYN summary.')).not.toHaveClass('ai-brief-clamped')
    expect(screen.getByRole('button', { name: 'Show less' })).toHaveAttribute('aria-expanded', 'true')
  })
})
