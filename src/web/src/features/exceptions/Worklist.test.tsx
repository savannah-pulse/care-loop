import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { exceptionItem, fakeApi, summary } from '../../test/fixtures'
import { Worklist } from './Worklist'

function renderWorklist(api: ReturnType<typeof fakeApi>, role = 'clinician') {
  return render(<Worklist api={api} identityKey="reader" role={role} onOpen={() => {}} />)
}

const rows = (count: number) => Array.from({ length: count }, (_, index) => summary(`case-${String(index).padStart(3, '0')}`))

describe('worklist', () => {
  it('disables the next page when a page ends exactly on the page size', async () => {
    const api = fakeApi({ cases: vi.fn().mockResolvedValue(rows(50)) })
    renderWorklist(api)
    await waitFor(() => expect(screen.getAllByRole('row')).toHaveLength(51))
    expect(api.cases).toHaveBeenCalledWith(expect.objectContaining({ skip: 0, take: 51 }), expect.anything())
    expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled()
  })

  it('offers the next page only when a further row exists, showing one page of rows', async () => {
    const user = userEvent.setup()
    const api = fakeApi({ cases: vi.fn().mockResolvedValue(rows(51)) })
    renderWorklist(api)
    await waitFor(() => expect(screen.getAllByRole('row')).toHaveLength(51))
    const next = screen.getByRole('button', { name: 'Next page' })
    expect(next).toBeEnabled()
    await user.click(next)
    await waitFor(() => expect(api.cases).toHaveBeenLastCalledWith(expect.objectContaining({ skip: 50, take: 51 }), expect.anything()))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Previous page' })).toBeEnabled())
  })

  it('marks the selected queue chip as pressed', async () => {
    const user = userEvent.setup()
    renderWorklist(fakeApi())
    const all = await screen.findByRole('button', { name: /All cases/ })
    expect(all).toHaveAttribute('aria-pressed', 'true')
    await user.click(screen.getByRole('button', { name: /Overdue/ }))
    expect(screen.getByRole('button', { name: /Overdue/ })).toHaveAttribute('aria-pressed', 'true')
    expect(all).toHaveAttribute('aria-pressed', 'false')
  })

  it('gives each row a distinct accessible name', async () => {
    renderWorklist(fakeApi({ cases: vi.fn().mockResolvedValue([summary('case-1'), summary('case-2')]) }))
    expect(await screen.findByRole('button', { name: 'Synthetic case-1' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Synthetic case-2' })).toBeInTheDocument()
  })

  it('searches patient references for staff but not for a coordinator, who never sees them', async () => {
    const user = userEvent.setup()
    const row = summary('case-9', { patientDisplayName: null, patientReference: 'Patient/SYN-9' })
    const staff = renderWorklist(fakeApi({ cases: vi.fn().mockResolvedValue([row]) }))
    await user.type(await screen.findByLabelText(/Filter this page by name, identifier or case reference/), 'Patient/SYN-9')
    expect(screen.getByRole('button', { name: 'case-9' })).toBeInTheDocument()
    staff.unmount()
    renderWorklist(fakeApi({ cases: vi.fn().mockResolvedValue([row]) }), 'coordinator')
    const box = await screen.findByLabelText('Filter this page by case reference')
    await user.type(box, 'Patient/SYN-9')
    expect(screen.getByText('No matching cases need your attention.')).toBeInTheDocument()
    await user.clear(box)
    await user.type(box, 'case-9')
    expect(screen.getByRole('button', { name: 'case-9' })).toBeInTheDocument()
  })

  it('shows localized text for an exception with no deadline or next action', async () => {
    const user = userEvent.setup()
    const item = exceptionItem('case-1', { nextAction: null, dueAt: null })
    renderWorklist(fakeApi({ exceptions: vi.fn().mockResolvedValue([item]) }))
    await user.click(await screen.findByRole('button', { name: /Needs a named owner/ }))
    expect(await screen.findByText('No deadline set')).toBeInTheDocument()
    expect(screen.getByText('No next action')).toBeInTheDocument()
  })
})
