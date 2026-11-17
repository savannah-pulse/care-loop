import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ApiOutcome, CallOutcome, CaseDetail } from '../../data'
import { caseDetail } from '../../testing/builders'
import { CallOutcomeCard } from './CallOutcomeCard'

const record = vi.hoisted(() =>
  vi.fn<(caseId: string, outcome: CallOutcome, reason?: string) => Promise<ApiOutcome<CaseDetail>>>(),
)

vi.mock('../../data', async (original) => ({
  ...(await original<typeof import('../../data')>()),
  recordCallOutcome: record,
}))

const detail = caseDetail({ caseId: 'case-1' })

describe('call outcome card', () => {
  beforeEach(() => {
    record.mockReset()
  })

  it('ticks nothing for the nurse and keeps the button off until something is ticked', async () => {
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={vi.fn()} />)
    const button = screen.getByRole('button', { name: 'Record call outcome' })
    expect(button).toBeDisabled()
    for (const box of screen.getAllByRole('checkbox')) {
      expect(box).not.toBeChecked()
    }
    expect(screen.getByRole('radio', { name: 'Not asked' })).toBeChecked()

    await userEvent.click(screen.getByRole('checkbox', { name: /said the next step back/ }))
    expect(button).toBeEnabled()
    expect(record).not.toHaveBeenCalled()
  })

  it('records exactly what was ticked and hands back the case the API returned', async () => {
    const updated = caseDetail({ caseId: 'case-1', confidence: 4 })
    record.mockResolvedValue({ ok: true, data: updated })
    const onRecorded = vi.fn()
    render(<CallOutcomeCard detail={detail} offer="open" reason="Covering clinic" onRecorded={onRecorded} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /said the next step back/ }))
    await userEvent.click(screen.getByRole('radio', { name: '4 out of 5' }))
    await userEvent.click(screen.getByRole('button', { name: 'Record call outcome' }))

    expect(record).toHaveBeenCalledExactlyOnceWith(
      'case-1',
      { acknowledged: false, understood: true, confidence: 4 },
      'Covering clinic',
    )
    expect(onRecorded).toHaveBeenCalledExactlyOnceWith(updated)
    expect(screen.getByRole('checkbox', { name: /said the next step back/ })).not.toBeChecked()
    expect(screen.getByRole('button', { name: 'Record call outcome' })).toBeDisabled()
  })

  it('keeps what was ticked, says why and has the timeline read again when the API refuses', async () => {
    record.mockResolvedValue({ ok: false, failure: { kind: 'conflict', status: 409 } })
    const onRecorded = vi.fn()
    const onConflict = vi.fn()
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={onRecorded} onConflict={onConflict} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /will come/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Record call outcome' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Not everything was recorded.')
    expect(screen.getByRole('alert')).toHaveTextContent('on the timeline below')
    expect(screen.getByRole('checkbox', { name: /will come/ })).toBeChecked()
    expect(onRecorded).not.toHaveBeenCalled()
    expect(onConflict).toHaveBeenCalledOnce()
  })

  it('does not read the timeline again for a refusal that saved nothing', async () => {
    record.mockResolvedValue({ ok: false, failure: { kind: 'forbidden', status: 403 } })
    const onConflict = vi.fn()
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={vi.fn()} onConflict={onConflict} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /will come/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Record call outcome' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Only a nurse navigator')
    expect(onConflict).not.toHaveBeenCalled()
  })

  it('shows the rating already on record', () => {
    render(<CallOutcomeCard detail={caseDetail({ confidence: 3 })} offer="open" onRecorded={vi.fn()} />)
    expect(screen.getByText('Confidence on record: 3 out of 5.')).toBeInTheDocument()
  })

  it('is locked, with the reason, until a message has been sent to the patient', () => {
    render(<CallOutcomeCard detail={detail} offer="notYet" onRecorded={vi.fn()} />)
    expect(screen.getByText(/no message has been sent to this patient/)).toBeInTheDocument()
    for (const control of [...screen.getAllByRole('checkbox'), ...screen.getAllByRole('radio')]) {
      expect(control).toBeDisabled()
    }
    expect(screen.getByRole('button', { name: 'Record call outcome' })).toBeDisabled()
  })
})
