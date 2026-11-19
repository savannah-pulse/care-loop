import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AiText, ApiOutcome, MessageChannel } from '../../data'
import { caseDetail } from '../../testing/builders'
import { PatientMessageCard } from './PatientMessageCard'

const draft = vi.hoisted(() =>
  vi.fn<(caseId: string, channel: MessageChannel, reason?: string) => Promise<ApiOutcome<AiText>>>(),
)

vi.mock('../../data', async (original) => ({
  ...(await original<typeof import('../../data')>()),
  draftPatientMessage: draft,
}))

const detail = caseDetail({ caseId: 'case-1' })

function written(text: string): ApiOutcome<AiText> {
  return { ok: true, data: { text, model: 'Foundry/patient-notification', generatedAt: '2026-11-12T06:30:00Z', cached: false } }
}

describe('patient message card', () => {
  beforeEach(() => {
    draft.mockReset()
  })

  it('asks for nothing until the reader asks, then shows the draft as a draft that was not sent', async () => {
    draft.mockResolvedValue(written('Dear {first name},\nYour care team would like to speak with you.'))
    render(<PatientMessageCard detail={detail} reason="SYN covering a colleague" />)

    expect(screen.getByRole('heading', { name: 'Draft a message to the patient' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Text message' })).toBeChecked()
    expect(draft).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    expect(draft).toHaveBeenCalledExactlyOnceWith('case-1', 'Sms', 'SYN covering a colleague')
    const shown = await screen.findByRole('group', { name: 'Draft message' })
    expect(within(shown).getByText(/Dear \{first name\},\s+Your care team would like to speak with you\./)).toBeInTheDocument()
    expect(within(shown).getByText(/^Written by AI · .* · Draft, not sent$/)).toBeInTheDocument()
    expect(shown).toHaveFocus()
    expect(screen.getByRole('button', { name: 'Write another draft' })).toBeInTheDocument()
  })

  it('asks for the channel the reader chose', async () => {
    draft.mockResolvedValue(written('SYN email draft'))
    render(<PatientMessageCard detail={detail} />)

    await userEvent.click(screen.getByRole('radio', { name: 'Email' }))
    await userEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    expect(draft).toHaveBeenCalledExactlyOnceWith('case-1', 'Email', undefined)
    expect(await screen.findByText('SYN email draft')).toBeInTheDocument()
  })

  it.each([
    ['conflict', 'AI is switched off here, so no draft can be written.'],
    ['forbidden', 'Your role cannot ask for a draft of this message.'],
    ['unavailable', 'AI could not write a draft just now. Nothing else is affected. Try again later.'],
  ] as const)('says why there is no draft when the API answers %s, and shows no stand-in text', async (kind, message) => {
    draft.mockResolvedValue({ ok: false, failure: { kind } })
    render(<PatientMessageCard detail={detail} />)

    await userEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
    expect(screen.queryByRole('group', { name: 'Draft message' })).not.toBeInTheDocument()
  })

  it('drops an earlier draft when a later request fails', async () => {
    draft.mockResolvedValueOnce(written('SYN first draft')).mockResolvedValueOnce({ ok: false, failure: { kind: 'unavailable' } })
    render(<PatientMessageCard detail={detail} />)

    await userEvent.click(screen.getByRole('button', { name: 'Write a draft' }))
    await screen.findByText('SYN first draft')
    await userEvent.click(screen.getByRole('button', { name: 'Write another draft' }))

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByText('SYN first draft')).not.toBeInTheDocument()
  })
})
