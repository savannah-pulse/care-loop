import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { answer, json, problem, sentRequests } from '../../testing/api'
import { aiText, caseDetail, timelineEntry } from '../../testing/builders'
import { renderApp } from '../../testing/screens'

const CASE = caseDetail()
const CASE_PAGE = `#/cases/${CASE.caseId}`
const DRAFT = `POST /cases/${CASE.caseId}/patient-message-draft`
const BUTTON = 'Preview a message to the patient'
const CAPTION = 'Draft written by AI. Not sent: messages go out from approved templates.'
const SMS_WORDS =
  '{first name}, this is your care team at Family Health Centre. We would like to talk with you. Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you.'
const EMAIL_WORDS = 'Subject: Your care team would like to talk with you\n\nDear {first name},\n\nWe would like to talk with you.'

function answerCase(detail = CASE): void {
  answer(`GET /cases/${detail.caseId}`, json(detail))
  answer(`GET /cases/${detail.caseId}/timeline`, json([timelineEntry()]))
  answer('GET /cases', json([]))
}

async function openPreview(as = 'huda-saleh') {
  answerCase()
  const app = renderApp({ at: CASE_PAGE, as })
  await app.user.click(await screen.findByRole('button', { name: BUTTON }))
  return { ...app, dialog: screen.getByRole('dialog', { name: BUTTON }) }
}

describe('preview a message to the patient', () => {
  it.each([
    ['the nurse navigator', 'huda-saleh', true],
    ['the clinician', 'rekha-varghese', true],
    ['the facility lead', 'joy-bautista', false],
    ['the programme coordinator', 'obaid-al-suwaidi', false],
  ])('is offered to %s: %s', async (_who, as, offered) => {
    answerCase()
    renderApp({ at: CASE_PAGE, as })
    await screen.findByRole('heading', { level: 2, name: 'Make sure the patient has the message' }).catch(() => undefined)
    await screen.findByRole('region', { name: 'How each part is going' })

    expect(screen.queryByRole('button', { name: BUTTON }) !== null).toBe(offered)
    expect(screen.queryByRole('region', { name: 'Message to the patient' }) !== null).toBe(offered)
    expect(sentRequests(DRAFT)).toHaveLength(0)
  })

  it('opens a dialog with the draft as a text message, captioned as AI and not sent, and moves focus in and back out', async () => {
    answer(DRAFT, (request) =>
      json(
        aiText({
          agent: 'patient-notification',
          agentVersion: '2',
          promptVersion: 'patient-message-v2',
          text: request.query.get('channel') === 'Email' ? EMAIL_WORDS : SMS_WORDS,
        }),
      ),
    )
    const { user, dialog } = await openPreview()

    const textMessage = within(dialog).getByRole('radio', { name: 'Text message' })
    expect(textMessage).toBeChecked()
    expect(textMessage).toHaveFocus()
    const words = await within(dialog).findByRole('figure', { name: CAPTION })
    expect(words).toHaveTextContent(SMS_WORDS)
    expect(words.textContent).not.toMatch(/Hessa|100245891/)
    expect(within(words).getByText('Agent patient-notification, version 2. Prompt patient-message-v2.')).toHaveClass('ai-versions')
    const [first] = sentRequests(DRAFT)
    expect(first.query.get('channel')).toBe('Sms')
    expect(first.query.has('reason')).toBe(false)
    expect(first.headers.get('X-Demo-User')).toBe('huda-saleh')
    expect(first.headers.get('X-Demo-Role')).toBe('nurse-navigator')
    expect(first.headers.get('X-Demo-Facility')).toBe('family-health-centre')

    await user.click(within(dialog).getByRole('radio', { name: 'Email' }))
    await waitFor(() => expect(within(dialog).getByRole('figure', { name: CAPTION })).toHaveTextContent('Subject: Your care team would like to talk with you'))
    expect(sentRequests(DRAFT).map((request) => request.query.get('channel'))).toEqual(['Sms', 'Email'])

    await user.click(within(dialog).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog', { name: BUTTON })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: BUTTON })).toHaveFocus()
  })

  it('shows a skeleton while AI writes, and closes with Escape', async () => {
    let release: (response: Response) => void = () => undefined
    answer(DRAFT, () => new Promise<Response>((resolve) => (release = resolve)))
    const { user, dialog } = await openPreview('rekha-varghese')

    expect(within(dialog).getByRole('status')).toHaveTextContent('Writing a preview')
    release(json(aiText({ agent: 'patient-notification', text: SMS_WORDS })))
    expect(await within(dialog).findByRole('figure', { name: CAPTION })).toBeInTheDocument()
    expect(sentRequests(DRAFT)[0].headers.get('X-Demo-Role')).toBe('clinician')

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: BUTTON })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: BUTTON })).toHaveFocus()
  })

  it('says in one calm line that it is not ready yet while AI is switched off (409)', async () => {
    answer(DRAFT, problem(409, 'PatientMessageDraft is switched off here.'))
    const { dialog } = await openPreview()

    expect(await within(dialog).findByText('Not ready yet. Previews will show here once AI is switched on.')).toBeInTheDocument()
    expect(within(dialog).queryByRole('figure')).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Write it again' })).not.toBeInTheDocument()
  })

  it('says calmly that AI could not write it (503) and nothing was sent, and can ask again', async () => {
    answer(DRAFT, problem(503, 'AI could not write this'))
    const { user, dialog } = await openPreview()

    expect(await within(dialog).findByText('AI could not write a preview this time. Nothing has been sent.')).toBeInTheDocument()
    expect(within(dialog).queryByRole('figure')).not.toBeInTheDocument()

    answer(DRAFT, json(aiText({ agent: 'patient-notification', text: SMS_WORDS })))
    await user.click(within(dialog).getByRole('button', { name: 'Write it again' }))
    expect(await within(dialog).findByRole('figure', { name: CAPTION })).toHaveTextContent(SMS_WORDS)
    expect(sentRequests(DRAFT)).toHaveLength(2)
  })
})
