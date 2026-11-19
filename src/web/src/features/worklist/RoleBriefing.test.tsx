import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { signInAs } from '../../identity'
import { answer, json, sentRequests } from '../../testing/api'
import { RoleBriefing } from './RoleBriefing'

const now = new Date()

describe('role briefing', () => {
  beforeEach(() => {
    signInAs('mona-khalil')
  })

  it('is read again when the worklist it describes changes, keeping the text on screen meanwhile and reusing the kept summary', async () => {
    let calls = 0
    answer('GET /ai/role-briefing', () => {
      calls += 1
      return json({ text: `SYN briefing ${calls}.`, model: 'Foundry/role-briefing', generatedAt: now.toISOString(), cached: false })
    })
    const { rerender } = render(<RoleBriefing now={now} snapshot="one case" />)
    expect(await screen.findByText('SYN briefing 1.')).toBeInTheDocument()

    // The same worklist read again in the background asks for nothing.
    rerender(<RoleBriefing now={now} snapshot="one case" />)
    expect(sentRequests('GET /ai/role-briefing')).toHaveLength(1)

    rerender(<RoleBriefing now={now} snapshot="two cases" />)

    // No "writing" line in between: the earlier text stays until the new one arrives.
    expect(screen.getByText('SYN briefing 1.')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(await screen.findByText('SYN briefing 2.')).toBeInTheDocument()
    expect(sentRequests('GET /ai/role-briefing').at(-1)?.query.has('refresh')).toBe(false)
  })

  it('asks for a new summary once on Refresh, not again when the worklist changes afterwards', async () => {
    answer('GET /ai/role-briefing', () =>
      json({ text: 'SYN briefing.', model: 'Foundry/role-briefing', generatedAt: now.toISOString(), cached: false }),
    )
    const { rerender } = render(<RoleBriefing now={now} snapshot="one case" />)
    await userEvent.click(await screen.findByRole('button', { name: 'Refresh' }))
    await vi.waitFor(() => expect(sentRequests('GET /ai/role-briefing')).toHaveLength(2))
    expect(sentRequests('GET /ai/role-briefing')[1].query.get('refresh')).toBe('true')
    await screen.findByRole('button', { name: 'Refresh' })

    rerender(<RoleBriefing now={now} snapshot="two cases" />)

    await vi.waitFor(() => expect(sentRequests('GET /ai/role-briefing')).toHaveLength(3))
    expect(sentRequests('GET /ai/role-briefing')[2].query.has('refresh')).toBe(false)
  })
})
