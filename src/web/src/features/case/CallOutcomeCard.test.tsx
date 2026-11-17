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

  it('ticks nothing for the nurse and keeps the button off, but focusable, until something is ticked', async () => {
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={vi.fn()} />)
    const button = screen.getByRole('button', { name: 'Record call outcome' })
    expect(button).toHaveAttribute('aria-disabled', 'true')
    expect(button).toBeEnabled()
    for (const box of screen.getAllByRole('checkbox')) {
      expect(box).not.toBeChecked()
    }
    expect(screen.getByRole('radio', { name: 'Not asked' })).toBeChecked()

    await userEvent.click(button)
    expect(record).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('checkbox', { name: /said the next step back/ }))
    expect(button).toHaveAttribute('aria-disabled', 'false')
    expect(record).not.toHaveBeenCalled()
  })

  it('records exactly what was ticked, says what was recorded and moves focus to the card heading', async () => {
    const updated = caseDetail({ caseId: 'case-1', confidence: 4 })
    record.mockResolvedValue({ ok: true, data: updated })
    const onRecorded = vi.fn()
    render(
      <CallOutcomeCard
        detail={detail}
        offer="open"
        reason="Covering clinic"
        onRecorded={onRecorded}
        now={() => new Date('2026-11-17T05:21:00Z')}
      />,
    )

    await userEvent.click(screen.getByRole('checkbox', { name: /will come/ }))
    await userEvent.click(screen.getByRole('checkbox', { name: /said the next step back/ }))
    await userEvent.click(screen.getByRole('radio', { name: '4 out of 5' }))
    await userEvent.click(screen.getByRole('button', { name: 'Record call outcome' }))

    expect(record).toHaveBeenCalledExactlyOnceWith(
      'case-1',
      { acknowledged: true, understood: true, confidence: 4 },
      'Covering clinic',
    )
    expect(onRecorded).toHaveBeenCalledExactlyOnceWith(updated)
    expect(screen.getByRole('status')).toHaveTextContent('Recorded at 09:21: will come, said the next step back, 4 out of 5.')
    expect(screen.getByRole('heading', { name: 'After a call with the patient' })).toHaveFocus()
    expect(screen.getByRole('checkbox', { name: /said the next step back/ })).not.toBeChecked()
  })

  it('keeps what was ticked, focuses the message and has the case read again when the case changed', async () => {
    record.mockResolvedValue({ ok: false, failure: { kind: 'conflict', status: 409 } })
    const onRecorded = vi.fn()
    const onConflict = vi.fn()
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={onRecorded} onConflict={onConflict} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /will come/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Record call outcome' }))

    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('The case changed while you were recording')
    expect(alert).toHaveTextContent('on the timeline below')
    expect(alert).toHaveFocus()
    expect(screen.getByRole('checkbox', { name: /will come/ })).toBeChecked()
    expect(onRecorded).not.toHaveBeenCalled()
    expect(onConflict).toHaveBeenCalledOnce()
  })

  it('does not read the case again for a refusal that saved nothing', async () => {
    record.mockResolvedValue({ ok: false, failure: { kind: 'forbidden', status: 403 } })
    const onConflict = vi.fn()
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={vi.fn()} onConflict={onConflict} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /will come/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Record call outcome' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Only a nurse navigator')
    expect(screen.getByRole('alert')).toHaveFocus()
    expect(onConflict).not.toHaveBeenCalled()
  })

  it('ties the scale hint to the rating', () => {
    render(<CallOutcomeCard detail={detail} offer="open" onRecorded={vi.fn()} />)
    expect(screen.getByRole('group', { name: /How sure/ })).toHaveAccessibleDescription('1 is not sure, 5 is very sure.')
  })

  it('shows the rating already on record', () => {
    render(<CallOutcomeCard detail={caseDetail({ confidence: 3 })} offer="open" onRecorded={vi.fn()} />)
    expect(screen.getByText('Confidence on record: 3 out of 5.')).toBeInTheDocument()
  })

  it('is locked, and says why first, until a message has been sent to the patient', () => {
    const { container } = render(<CallOutcomeCard detail={detail} offer="notYet" onRecorded={vi.fn()} />)
    const reason = screen.getByText(/no message has been sent to this patient/)
    const form = container.querySelector('form')
    expect(form).toHaveAccessibleDescription(reason.textContent)
    expect(reason.compareDocumentPosition(form!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    for (const control of [...screen.getAllByRole('checkbox'), ...screen.getAllByRole('radio')]) {
      expect(control).toBeDisabled()
    }
    expect(screen.getByRole('button', { name: 'Record call outcome' })).toHaveAttribute('aria-disabled', 'true')
  })
})
