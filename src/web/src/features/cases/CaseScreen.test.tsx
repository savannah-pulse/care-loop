import { useState } from 'react'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import type { StaffIdentity } from '../../api/client'
import { clinician, coordinator, detail, facilityLead, fakeApi, handoffSummary, summary, timelineEntry } from '../../test/fixtures'
import type { MockApi } from '../../test/fixtures'
import { CaseScreen } from './CaseScreen'
import type { CaseView } from './CaseScreen'

function Harness({ api, identity, initial = 'case' }: { api: MockApi; identity: StaffIdentity; initial?: CaseView }) {
  const [view, setView] = useState<CaseView>(initial)
  return <CaseScreen api={api} identity={identity} identityKey={JSON.stringify(identity)} caseId="case-1" reason=""
    view={view} onView={setView} onOpen={() => {}} onBack={() => {}} />
}

const acknowledged = () => detail({
  escalations: [{ ...detail().escalations[0], acknowledgedAt: '2026-01-02T01:00:00Z', acknowledgedBy: 'SYN-LEAD-1' }],
})

describe('case screen', () => {
  it('shows a coordinator the case id and no names or patient references', async () => {
    const api = fakeApi({ detail: vi.fn().mockResolvedValue(detail({ patientDisplayName: null, patientSecondaryIdentifier: null })) })
    render(<Harness api={api} identity={coordinator} />)
    expect(await screen.findByText('case-1')).toBeInTheDocument()
    expect(document.body).not.toHaveTextContent('Patient/SYN-1')
    expect(document.body).not.toHaveTextContent('Synthetic Patient')
    expect(api.cases).not.toHaveBeenCalled()
  })

  it('offers acknowledgement only to the addressed role, after explicit confirmation', async () => {
    const user = userEvent.setup()
    const lead = render(<Harness api={fakeApi()} identity={facilityLead} initial="escalations" />)
    const button = await screen.findByRole('button', { name: 'Acknowledge' })
    expect(button).toBeDisabled()
    await user.click(screen.getByRole('checkbox'))
    expect(button).toBeEnabled()
    lead.unmount()
    render(<Harness api={fakeApi()} identity={clinician} initial="escalations" />)
    await screen.findByText('Synthetic overdue reason')
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
  })

  it('uses the acknowledgement response and refetches only the timeline without blanking the screen', async () => {
    const user = userEvent.setup()
    let releaseTimeline: (value: unknown[]) => void = () => {}
    const api = fakeApi({ acknowledge: vi.fn().mockResolvedValue(acknowledged()) })
    render(<Harness api={api} identity={facilityLead} initial="escalations" />)
    await user.click(await screen.findByRole('checkbox'))
    api.timeline.mockReturnValueOnce(new Promise((resolve) => { releaseTimeline = resolve }))
    await user.click(screen.getByRole('button', { name: 'Acknowledge' }))
    await waitFor(() => expect(api.timeline).toHaveBeenCalledTimes(2))
    expect(screen.getByText(/Acknowledged: SYN-LEAD-1/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Escalation oversight' })).toBeInTheDocument()
    releaseTimeline([timelineEntry])
    expect(api.detail).toHaveBeenCalledTimes(1)
    expect(api.programme).toHaveBeenCalledTimes(1)
  })

  it('clears a failed acknowledgement message when the case is refreshed', async () => {
    const user = userEvent.setup()
    const api = fakeApi({ acknowledge: vi.fn().mockRejectedValue(new ApiError(409)) })
    render(<Harness api={api} identity={facilityLead} initial="escalations" />)
    await user.click(await screen.findByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Acknowledge' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('The record changed')
    await user.click(screen.getByRole('button', { name: 'Refresh' }))
    await waitFor(() => expect(api.detail).toHaveBeenCalledTimes(2))
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  })

  it('keeps owner, deadline, timeline and acknowledgement when only the programme read fails', async () => {
    const user = userEvent.setup()
    const api = fakeApi({ programme: vi.fn().mockRejectedValue(new ApiError(404)) })
    render(<Harness api={api} identity={facilityLead} />)
    expect(await screen.findByText('SYN-STAFF-1')).toBeInTheDocument()
    expect(await screen.findByText('Synthetic result')).toBeInTheDocument()
    expect(screen.getByText(/pathway definition could not be read/)).toBeInTheDocument()
    expect(screen.queryByText('Tracks required for closure')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Escalations' }))
    expect(screen.getByText(/pathway definition could not be read/)).toBeInTheDocument()
    await user.click(await screen.findByRole('checkbox'))
    expect(screen.getByRole('button', { name: 'Acknowledge' })).toBeEnabled()
  })

  it('marks the selected tab for assistive technology', async () => {
    render(<Harness api={fakeApi()} identity={clinician} />)
    const tab = await screen.findByRole('button', { name: 'Case' })
    expect(tab).toHaveAttribute('aria-current', 'true')
    expect(screen.getByRole('button', { name: 'Escalations' })).not.toHaveAttribute('aria-current')
  })

  it('shows no deadline text when an item has none', async () => {
    const api = fakeApi({ detail: vi.fn().mockResolvedValue(detail({ dueAt: null, nextAction: null })) })
    render(<Harness api={api} identity={clinician} />)
    expect(await screen.findByText('No deadline set')).toBeInTheDocument()
    expect(screen.getByText('No next action')).toBeInTheDocument()
  })
})

describe('handoff summary review', () => {
  async function draft(api: MockApi) {
    const user = userEvent.setup()
    render(<Harness api={api} identity={clinician} initial="summary" />)
    await user.click(await screen.findByRole('button', { name: 'Request a draft' }))
    return { user, textarea: await screen.findByRole('textbox', { name: 'Text to approve' }) as HTMLTextAreaElement }
  }

  it('sends exactly the textarea text on approval', async () => {
    const api = fakeApi()
    const { user, textarea } = await draft(api)
    const text = '  Reviewed by a person.\n\n Next step unchanged.  '
    fireEvent.change(textarea, { target: { value: text } })
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Approve the exact text' }))
    await waitFor(() => expect(api.approve).toHaveBeenCalledWith('case-1', 'draft-1', text))
  })

  it('gates approval on the confirmation, and edits withdraw it', async () => {
    const { user, textarea } = await draft(fakeApi())
    const approve = screen.getByRole('button', { name: 'Approve the exact text' })
    expect(approve).toBeDisabled()
    await user.click(screen.getByRole('checkbox'))
    expect(approve).toBeEnabled()
    fireEvent.change(textarea, { target: { value: 'Edited after confirming.' } })
    expect(screen.getByRole('checkbox')).not.toBeChecked()
    expect(approve).toBeDisabled()
  })

  it('keeps the draft, edits and its id across tab switches and Refresh', async () => {
    const api = fakeApi()
    const { user, textarea } = await draft(api)
    fireEvent.change(textarea, { target: { value: 'Edited but not yet approved.' } })
    await user.click(screen.getByRole('button', { name: 'Case' }))
    await user.click(screen.getByRole('button', { name: 'Handoff summary' }))
    expect(screen.getByRole('textbox', { name: 'Text to approve' })).toHaveValue('Edited but not yet approved.')
    await user.click(screen.getByRole('button', { name: 'Refresh' }))
    await waitFor(() => expect(api.detail).toHaveBeenCalledTimes(2))
    expect(screen.getByRole('textbox', { name: 'Text to approve' })).toHaveValue('Edited but not yet approved.')
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Approve the exact text' }))
    await waitFor(() => expect(api.approve).toHaveBeenCalledWith('case-1', 'draft-1', 'Edited but not yet approved.'))
  })

  it('still applies a draft that arrives after the tab was switched away', async () => {
    const user = userEvent.setup()
    let release: (value: unknown) => void = () => {}
    const api = fakeApi({ draft: vi.fn().mockReturnValue(new Promise((resolve) => { release = resolve })) })
    render(<Harness api={api} identity={clinician} initial="summary" />)
    await user.click(await screen.findByRole('button', { name: 'Request a draft' }))
    await user.click(screen.getByRole('button', { name: 'Case' }))
    release(handoffSummary({ text: 'Late draft.' }))
    await user.click(screen.getByRole('button', { name: 'Handoff summary' }))
    expect(await screen.findByRole('textbox', { name: 'Text to approve' })).toHaveValue('Late draft.')
  })
})

describe('other cases', () => {
  async function otherCases(count: number) {
    const rows = Array.from({ length: count }, (_, index) => summary(`other-${index}`))
    const api = fakeApi({ cases: vi.fn().mockResolvedValue(rows) })
    render(<Harness api={api} identity={clinician} />)
    await screen.findByText('Other screening cases')
    await waitFor(() => expect(api.cases).toHaveBeenCalled())
    return api
  }

  it('enables the next page only when a following row exists', async () => {
    const exactlyOnePage = await otherCases(50)
    expect(exactlyOnePage.cases).toHaveBeenCalledWith(expect.objectContaining({ skip: 0, take: 51 }), expect.anything())
    const aside = within(screen.getByRole('complementary'))
    await waitFor(() => expect(aside.getAllByRole('listitem')).toHaveLength(50))
    expect(aside.getByRole('button', { name: 'Next page' })).toBeDisabled()
  })

  it('enables the next page when more than a page exists', async () => {
    await otherCases(51)
    const aside = within(screen.getByRole('complementary'))
    await waitFor(() => expect(aside.getAllByRole('listitem')).toHaveLength(50))
    expect(aside.getByRole('button', { name: 'Next page' })).toBeEnabled()
  })
})
