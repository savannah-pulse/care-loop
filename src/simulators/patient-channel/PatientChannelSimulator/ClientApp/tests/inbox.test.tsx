import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { InboxPage, inboxOf } from '../src/features/inbox';
import en from '../src/locales/en.json';

import { emailMessage, inboxMessage, mockApi, problem, renderAt } from './helpers';

const records = en.inbox.records;
const phoneLabel = en.inbox.sms.deviceLabel;
const mailLabel = en.inbox.email.deviceLabel;

/** The buttons of the message picker, in list order. */
function pickerButtons() {
  return within(screen.getByRole('list', { name: en.inbox.messagesLabel })).getAllByRole('button');
}

/** The titles of the reply records, in order. */
function replyStates() {
  return screen.getAllByTestId('reply-state').map((element) => element.textContent);
}

describe('Inboxes', () => {
  it('lists simulated messages newest first with a simulated marker', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({
          messageId: 'newest',
          displayName: 'Synthetic patient B',
          body: 'SIMULATED B',
        }),
        inboxMessage({
          messageId: 'oldest',
          displayName: 'Synthetic patient A',
          body: 'SIMULATED A',
        }),
      ],
    });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: phoneLabel });
    expect(pickerButtons().map((button) => button.querySelector('b')?.textContent)).toEqual([
      'Synthetic patient B',
      'Synthetic patient A',
    ]);
    expect(pickerButtons()[0]?.getAttribute('aria-pressed')).toBe('true');
    expect(screen.getByText('SIMULATED B')).toBeTruthy();
    expect(screen.getByText(en.common.simulated)).toBeTruthy();

    fireEvent.click(pickerButtons()[1] as HTMLElement);
    expect(screen.getByText('SIMULATED A')).toBeTruthy();
  });

  it('offers only text message and email inboxes: no health app and no portal', async () => {
    mockApi({ 'GET /api/inbox': [inboxMessage()] });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: phoneLabel });
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      'Patient text messages (1)',
      'Patient email (0)',
      'Caregiver (0)',
      'Staff (0)',
    ]);
    expect(screen.queryByText(/health app/i)).toBeNull();
    expect(screen.queryByText(/portal/i)).toBeNull();
    expect(screen.queryByRole('link')).toBeNull();
  });

  it('files each message under the patient by channel, the caregiver or staff', () => {
    expect(inboxOf(inboxMessage())).toBe('PatientSms');
    expect(inboxOf(emailMessage())).toBe('PatientEmail');
    expect(inboxOf(inboxMessage({ recipientKind: 'Caregiver' }))).toBe('Caregiver');
    expect(inboxOf(emailMessage({ recipientKind: 'Caregiver' }))).toBe('Caregiver');
    expect(inboxOf(emailMessage({ recipientKind: 'Staff' }))).toBe('Staff');
  });

  it('shows the first inbox that has a message, and another when its tab is chosen', async () => {
    mockApi({
      'GET /api/inbox': [
        emailMessage({ messageId: 'mail' }),
        emailMessage({
          messageId: 'notice',
          recipientKind: 'Staff',
          displayName: 'Synthetic staff',
        }),
      ],
    });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: mailLabel });
    expect(
      screen.getByRole('tab', { name: 'Patient email (1)' }).getAttribute('aria-selected'),
    ).toBe('true');

    fireEvent.click(screen.getByRole('tab', { name: 'Staff (1)' }));
    expect(screen.getAllByText(/Synthetic staff/).length).toBeGreaterThan(0);
    expect(screen.getByText(en.inbox.readOnly.Staff)).toBeTruthy();

    fireEvent.click(screen.getByRole('tab', { name: 'Caregiver (0)' }));
    expect(screen.getByText(en.inbox.empty)).toBeTruthy();
  });

  it('shows the delivery state and that a delivered message is not acknowledged', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({ messageId: 'delivered' }),
        inboxMessage({
          messageId: 'failed',
          delivery: 'Failed',
          deliveryFailureReason: 'SIMULATED: unreachable',
        }),
      ],
    });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: phoneLabel });
    expect(screen.getByTestId('delivery-state').textContent).toBe(records.delivery.Delivered);
    expect(screen.getByText(records.deliveryDetail.Delivered)).toBeTruthy();
    expect(replyStates()).toEqual([records.noReply]);

    fireEvent.click(pickerButtons()[1] as HTMLElement);
    expect(screen.getByTestId('delivery-state').textContent).toBe(records.delivery.Failed);
    expect(screen.getByText('SIMULATED: unreachable')).toBeTruthy();
  });

  it('a text message is answered by keying 1 and pressing Reply', async () => {
    const replied = inboxMessage({
      replies: [{ replyId: 'message-1-r1', text: '1', sentAt: '2026-03-02T09:31:00+00:00' }],
    });
    const api = mockApi({
      'GET /api/inbox': [inboxMessage()],
      'POST /api/inbox/message-1/reply': replied,
    });
    renderAt(<InboxPage />);
    const phone = await screen.findByRole('group', { name: phoneLabel });
    const reply = within(phone).getByRole('button', {
      name: en.inbox.sms.reply,
    }) as HTMLButtonElement;
    expect(reply.disabled).toBe(true);

    fireEvent.click(within(phone).getByRole('button', { name: 'Key 1' }));
    expect(screen.getByTestId('sms-draft').textContent).toBe('Reply: 1');
    fireEvent.click(reply);

    await waitFor(() => expect(replyStates()).toEqual(['Replied "1"']));
    expect(screen.getByText(records.meaning.WillCome)).toBeTruthy();
    expect(api.callsTo('POST /api/inbox/message-1/reply')[0]?.body).toEqual({ text: '1' });
    expect(screen.getByTestId('sms-draft').textContent).toBe('');
  });

  it('any keyed reply is sent as typed, and one that is not 1 is shown as a call request', async () => {
    const api = mockApi({
      'GET /api/inbox': [inboxMessage()],
      'POST /api/inbox/message-1/reply': (call: { body: unknown }) =>
        inboxMessage({
          replies: [
            {
              replyId: 'message-1-r1',
              text: (call.body as { text: string }).text,
              sentAt: '2026-03-02T09:31:00+00:00',
            },
          ],
        }),
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Key 7' }));
    fireEvent.click(screen.getByRole('button', { name: 'Key 3' }));
    fireEvent.click(screen.getByRole('button', { name: en.inbox.sms.reply }));

    await waitFor(() => expect(replyStates()).toEqual(['Replied "73"']));
    expect(api.callsTo('POST /api/inbox/message-1/reply')[0]?.body).toEqual({ text: '73' });
    expect(screen.getByText(records.meaning.Other)).toBeTruthy();
    expect(screen.queryByText(records.meaning.WillCome)).toBeNull();
  });

  it('a reply of 2 is shown as a call request, never as an acknowledgement', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({
          replies: [{ replyId: 'message-1-r1', text: ' 2 ', sentAt: '2026-03-02T09:31:00+00:00' }],
        }),
      ],
    });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: phoneLabel });
    expect(screen.getByText(records.meaning.CallMe)).toBeTruthy();
    expect(screen.queryByText(records.meaning.WillCome)).toBeNull();
  });

  it('Back clears a mistyped reply before anything is sent', async () => {
    const api = mockApi({ 'GET /api/inbox': [inboxMessage()] });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Key 7' }));
    fireEvent.click(screen.getByRole('button', { name: en.inbox.sms.back }));

    expect(screen.getByTestId('sms-draft').textContent).toBe('');
    expect(api.calls.every((call) => call.method === 'GET')).toBe(true);
  });

  it('a patient email shows its subject and is answered in the reply box', async () => {
    const replied = emailMessage({
      replies: [
        { replyId: 'message-1-r1', text: 'Please call me', sentAt: '2026-03-02T09:31:00+00:00' },
      ],
    });
    const api = mockApi({
      'GET /api/inbox': [emailMessage()],
      'POST /api/inbox/message-1/reply': replied,
    });
    renderAt(<InboxPage />);
    const mail = await screen.findByRole('group', { name: mailLabel });
    expect(screen.getByTestId('email-subject').textContent).toBe(
      'A follow-up step is waiting for you',
    );
    expect(within(mail).queryByRole('link')).toBeNull();
    const send = within(mail).getByRole('button', {
      name: en.inbox.email.send,
    }) as HTMLButtonElement;
    expect(send.disabled).toBe(true);

    const box = within(mail).getByRole('textbox', { name: en.inbox.email.replyLabel });
    fireEvent.change(box, { target: { value: 'Please call me' } });
    fireEvent.click(send);

    await waitFor(() => expect(replyStates()).toEqual(['Replied "Please call me"']));
    expect(api.callsTo('POST /api/inbox/message-1/reply')[0]?.body).toEqual({
      text: 'Please call me',
    });
    expect((box as HTMLTextAreaElement).value).toBe('');
    expect(screen.getByText(records.meaning.Other)).toBeTruthy();
  });

  it('keeps the typed email reply and shows an error when CareLoop cannot be reached', async () => {
    mockApi({
      'GET /api/inbox': [emailMessage()],
      'POST /api/inbox/message-1/reply': problem(
        502,
        'CareLoop could not be reached.',
        'SIMULATED: connection refused',
      ),
    });
    renderAt(<InboxPage />);
    const box = await screen.findByRole('textbox', { name: en.inbox.email.replyLabel });

    fireEvent.change(box, { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: en.inbox.email.send }));

    expect((await screen.findByRole('alert')).textContent).toContain(
      'CareLoop could not be reached.',
    );
    expect(replyStates()).toEqual([records.noReply]);
    expect((box as HTMLTextAreaElement).value).toBe('1');
  });

  it('shows an error when the inbox cannot be loaded', async () => {
    mockApi({});

    renderAt(<InboxPage />);

    expect((await screen.findByRole('alert')).textContent).toContain('Unexpected request');
  });

  it('a message that did not reach the patient cannot be replied to', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({ delivery: 'Failed', deliveryFailureReason: 'SIMULATED: unreachable' }),
        emailMessage({ messageId: 'mail', delivery: 'Pending' }),
      ],
    });

    renderAt(<InboxPage />);

    const phone = await screen.findByRole('group', { name: phoneLabel });
    for (const key of within(phone).getAllByRole('button')) {
      expect((key as HTMLButtonElement).disabled).toBe(true);
    }
    expect(screen.getByText(en.inbox.notAnswerable)).toBeTruthy();

    fireEvent.click(screen.getByRole('tab', { name: 'Patient email (1)' }));
    const mail = screen.getByRole('group', { name: mailLabel });
    expect((within(mail).getByRole('textbox') as HTMLTextAreaElement).disabled).toBe(true);
    expect(screen.getByText(en.inbox.notAnswerable)).toBeTruthy();
  });

  it('a caregiver email is read-only', async () => {
    const api = mockApi({
      'GET /api/inbox': [
        emailMessage({
          messageId: 'caregiver-message-1',
          recipientKind: 'Caregiver',
          to: 'SYN-CG-EMAIL-0001',
          displayName: 'Synthetic caregiver SYN-CG-EMAIL-0001',
          subject: 'An appointment has been booked',
        }),
      ],
    });

    renderAt(<InboxPage />);

    const email = await screen.findByRole('group', { name: mailLabel });
    expect(screen.getByRole('tab', { name: 'Caregiver (1)' }).getAttribute('aria-selected')).toBe(
      'true',
    );
    expect(within(email).getByText(/Synthetic caregiver SYN-CG-EMAIL-0001/)).toBeTruthy();
    expect(screen.getByText(en.inbox.readOnly.Caregiver)).toBeTruthy();
    expect(within(email).queryByRole('button')).toBeNull();
    expect(within(email).queryByRole('textbox')).toBeNull();
    expect(within(email).queryByRole('link')).toBeNull();
    expect(screen.queryByTestId('reply-state')).toBeNull();
    expect(screen.queryByTestId('nurse-note')).toBeNull();
    expect(screen.getByTestId('read-only-note').textContent).toBe(records.onlyPatient);
    expect(screen.getByText(records.onlyPatientDetail.Caregiver)).toBeTruthy();
    expect(api.calls.every((call) => call.method === 'GET')).toBe(true);
  });

  it('a staff text message is shown on the phone with the keypad locked', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({
          recipientKind: 'Staff',
          to: 'SYN-TEL-NURSES',
          displayName: 'Synthetic staff SYN-TEL-NURSES',
          body: 'SIMULATED: a case needs your action.',
        }),
      ],
    });

    renderAt(<InboxPage />);

    const phone = await screen.findByRole('group', { name: phoneLabel });
    expect(screen.getByRole('tab', { name: 'Staff (1)' }).getAttribute('aria-selected')).toBe(
      'true',
    );
    for (const key of within(phone).getAllByRole('button')) {
      expect((key as HTMLButtonElement).disabled).toBe(true);
    }
    expect(screen.getByText(en.inbox.readOnly.Staff)).toBeTruthy();
    expect(screen.queryByText(en.inbox.notAnswerable)).toBeNull();
    expect(screen.getByText(records.onlyPatientDetail.Staff)).toBeTruthy();
  });

  it('says understanding and confidence are recorded by a nurse, and offers no way to enter them', async () => {
    mockApi({ 'GET /api/inbox': [emailMessage()] });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: mailLabel });
    expect(screen.getByTestId('nurse-note').textContent).toBe(records.byNurse);
    expect(screen.getByText(records.byNurseDetail)).toBeTruthy();
    expect(screen.queryByRole('radio')).toBeNull();
    expect(screen.queryByRole('button', { name: /rate|understand|will come|call me/i })).toBeNull();
  });
});
