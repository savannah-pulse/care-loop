import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { InboxPage } from '../src/features/inbox';
import en from '../src/locales/en.json';

import { inboxMessage, mockApi, problem, renderAt } from './helpers';

const app = en.inbox.app;
const records = en.inbox.records;

/** The buttons of the message picker, in list order. */
function pickerButtons() {
  return within(screen.getByRole('list', { name: en.inbox.messagesLabel })).getAllByRole('button');
}

describe('Patient inbox', () => {
  it('lists simulated messages newest first with a simulated marker', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({ messageId: 'newest', patientDisplayName: 'Synthetic patient B' }),
        inboxMessage({ messageId: 'oldest', patientDisplayName: 'Synthetic patient A' }),
      ],
    });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: en.inbox.channel.App });
    expect(pickerButtons().map((button) => button.querySelector('b')?.textContent)).toEqual([
      'Synthetic patient B',
      'Synthetic patient A',
    ]);
    expect(pickerButtons()[0]?.getAttribute('aria-pressed')).toBe('true');
    expect(screen.getByText('Hello, Synthetic patient B')).toBeTruthy();
    expect(screen.getByText(en.common.simulated)).toBeTruthy();

    fireEvent.click(pickerButtons()[1] as HTMLElement);
    expect(screen.getByText('Hello, Synthetic patient A')).toBeTruthy();
  });

  it('shows channel, delivery state and response state for each message', async () => {
    mockApi({
      'GET /api/inbox': [
        inboxMessage({ messageId: 'app', response: 'Understood', confidence: 4 }),
        inboxMessage({
          messageId: 'portal',
          channel: 'Portal',
          delivery: 'Failed',
          deliveryFailureReason: 'SIMULATED: unreachable',
        }),
      ],
    });

    renderAt(<InboxPage />);

    await screen.findByRole('group', { name: en.inbox.channel.App });
    expect(screen.getByRole('tab', { name: 'Health app (1)' }).getAttribute('aria-selected')).toBe(
      'true',
    );
    expect(screen.getByTestId('delivery-state').textContent).toBe(records.delivery.Delivered);
    expect(screen.getByTestId('response-state').textContent).toBe(records.response.Understood);
    expect(screen.getByTestId('confidence-state').textContent).toBe('Confidence 4 out of 5');

    fireEvent.click(screen.getByRole('tab', { name: 'Patient portal (1)' }));
    const portal = screen.getByRole('group', { name: en.inbox.channel.Portal });
    expect(within(portal).getByText(app.appName.Portal)).toBeTruthy();
    expect(screen.getByTestId('delivery-state').textContent).toBe(records.delivery.Failed);
    expect(screen.getByText('SIMULATED: unreachable')).toBeTruthy();
    expect(within(portal).getByText(en.inbox.notAnswerable)).toBeTruthy();
    expect(within(portal).queryByRole('button')).toBeNull();
  });

  it('shows the message exactly as CareLoop worded it', async () => {
    const body = 'SIMULATED\n  More tests on Friday at 9:00 am.  ';
    mockApi({ 'GET /api/inbox': [inboxMessage({ body })] });

    renderAt(<InboxPage />);

    const phone = await screen.findByRole('group', { name: en.inbox.channel.App });
    expect(phone.querySelector('.message-body')?.textContent).toBe(body);
  });

  it('"I will come" calls POST /api/inbox/{id}/acknowledge, sends nothing else and thanks the patient', async () => {
    const api = mockApi({
      'GET /api/inbox': [inboxMessage()],
      'POST /api/inbox/message-1/acknowledge': inboxMessage({ response: 'Acknowledged' }),
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: app.acknowledge }));

    await waitFor(() =>
      expect(screen.getByTestId('response-state').textContent).toBe(records.response.Acknowledged),
    );
    expect(api.calls.filter((call) => call.method === 'POST')).toEqual([
      { method: 'POST', path: '/api/inbox/message-1/acknowledge', body: undefined },
    ]);
    expect(screen.getByText('Thank you, Synthetic patient SYN-PT-0001')).toBeTruthy();
    expect(screen.getByText(app.thanksAcknowledged)).toBeTruthy();
    expect(screen.getByText(app.teachBackQuestion)).toBeTruthy();
  });

  it.each([[app.anotherTime], [app.callMe]])(
    '"%s" calls POST /api/inbox/{id}/call-me and shows the call-requested state',
    async (button) => {
      const api = mockApi({
        'GET /api/inbox': [inboxMessage()],
        'POST /api/inbox/message-1/call-me': inboxMessage({ response: 'CallRequested' }),
      });
      renderAt(<InboxPage />);

      fireEvent.click(await screen.findByRole('button', { name: button }));

      await waitFor(() =>
        expect(screen.getByTestId('response-state').textContent).toBe(
          records.response.CallRequested,
        ),
      );
      expect(api.callsTo('POST /api/inbox/message-1/call-me')).toHaveLength(1);
      expect(screen.getByText(app.thanksCallRequested)).toBeTruthy();
    },
  );

  it('saying the next step back calls POST /api/inbox/{id}/understand and shows the understood state', async () => {
    const api = mockApi({
      'GET /api/inbox': [inboxMessage({ response: 'Acknowledged' })],
      'POST /api/inbox/message-1/understand': inboxMessage({ response: 'Understood' }),
    });
    renderAt(<InboxPage />);
    const send = (await screen.findByRole('button', { name: app.send })) as HTMLButtonElement;
    expect(send.disabled).toBe(true);

    fireEvent.click(screen.getByRole('radio', { name: app.answer.understand }));
    fireEvent.click(send);

    await waitFor(() =>
      expect(screen.getByTestId('response-state').textContent).toBe(records.response.Understood),
    );
    expect(api.calls.filter((call) => call.method === 'POST')).toHaveLength(1);
  });

  it.each([[app.answer.wait], [app.answer.unsure]])(
    'answering "%s" asks for a call and is not sent as understood',
    async (answer) => {
      const api = mockApi({
        'GET /api/inbox': [inboxMessage({ response: 'Acknowledged' })],
        'POST /api/inbox/message-1/call-me': inboxMessage({ response: 'CallRequested' }),
      });
      renderAt(<InboxPage />);

      fireEvent.click(await screen.findByRole('radio', { name: answer }));
      fireEvent.click(screen.getByRole('button', { name: app.send }));

      await waitFor(() => expect(api.callsTo('POST /api/inbox/message-1/call-me')).toHaveLength(1));
      expect(api.calls.some((call) => call.path.endsWith('/understand'))).toBe(false);
    },
  );

  it('a rating is sent with Send and does not change the response state', async () => {
    const api = mockApi({
      'GET /api/inbox': [inboxMessage({ response: 'Acknowledged' })],
      'POST /api/inbox/message-1/rate-confidence': inboxMessage({
        response: 'Acknowledged',
        confidence: 4,
      }),
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Rate 4 out of 5' }));
    expect(api.calls.some((call) => call.method === 'POST')).toBe(false);
    fireEvent.click(screen.getByRole('button', { name: app.send }));

    await waitFor(() =>
      expect(screen.getByTestId('confidence-state').textContent).toBe('Confidence 4 out of 5'),
    );
    expect(api.callsTo('POST /api/inbox/message-1/rate-confidence')[0]?.body).toEqual({
      rating: 4,
    });
    expect(screen.getByTestId('response-state').textContent).toBe(records.response.Acknowledged);
    expect(
      screen.getByRole('button', { name: 'Rate 4 out of 5' }).getAttribute('aria-pressed'),
    ).toBe('true');
  });

  it('Send reports the answer first and then the rating', async () => {
    const api = mockApi({
      'GET /api/inbox': [inboxMessage({ response: 'Acknowledged' })],
      'POST /api/inbox/message-1/understand': inboxMessage({ response: 'Understood' }),
      'POST /api/inbox/message-1/rate-confidence': inboxMessage({
        response: 'Understood',
        confidence: 5,
      }),
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('radio', { name: app.answer.understand }));
    fireEvent.click(screen.getByRole('button', { name: 'Rate 5 out of 5' }));
    fireEvent.click(screen.getByRole('button', { name: app.send }));

    await waitFor(() =>
      expect(screen.getByTestId('confidence-state').textContent).toBe('Confidence 5 out of 5'),
    );
    expect(api.calls.filter((call) => call.method === 'POST').map((call) => call.path)).toEqual([
      '/api/inbox/message-1/understand',
      '/api/inbox/message-1/rate-confidence',
    ]);
  });

  it('a retry after a failed rating does not ask for a second call', async () => {
    let ratingAttempts = 0;
    const api = mockApi({
      'GET /api/inbox': [inboxMessage({ response: 'Acknowledged' })],
      'POST /api/inbox/message-1/call-me': inboxMessage({ response: 'CallRequested' }),
      'POST /api/inbox/message-1/rate-confidence': () => {
        ratingAttempts += 1;
        return ratingAttempts === 1
          ? problem(502, 'CareLoop could not be reached.', 'SIMULATED outage.')
          : inboxMessage({ response: 'CallRequested', confidence: 3 });
      },
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('radio', { name: app.answer.wait }));
    fireEvent.click(screen.getByRole('button', { name: 'Rate 3 out of 5' }));
    fireEvent.click(screen.getByRole('button', { name: app.send }));
    await screen.findByRole('alert');

    fireEvent.click(screen.getByRole('button', { name: app.send }));
    await waitFor(() =>
      expect(screen.getByTestId('confidence-state').textContent).toBe('Confidence 3 out of 5'),
    );
    expect(api.calls.filter((call) => call.method === 'POST').map((call) => call.path)).toEqual([
      '/api/inbox/message-1/call-me',
      '/api/inbox/message-1/rate-confidence',
      '/api/inbox/message-1/rate-confidence',
    ]);
  });

  it('a delivered message is shown as not yet acknowledged', async () => {
    mockApi({ 'GET /api/inbox': [inboxMessage({ delivery: 'Delivered', response: 'None' })] });

    renderAt(<InboxPage />);

    expect((await screen.findByTestId('response-state')).textContent).toBe(records.response.None);
    expect(screen.getByTestId('delivery-state').textContent).toBe(records.delivery.Delivered);
    expect(screen.getByText(records.deliveryDetail.Delivered)).toBeTruthy();
    expect(screen.getByRole('button', { name: app.acknowledge })).toBeTruthy();
  });

  it('shows an error when CareLoop cannot be reached', async () => {
    mockApi({
      'GET /api/inbox': [inboxMessage()],
      'POST /api/inbox/message-1/acknowledge': problem(
        502,
        'CareLoop could not be reached.',
        'SIMULATED: connection refused',
      ),
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: app.acknowledge }));

    expect((await screen.findByRole('alert')).textContent).toContain(
      'CareLoop could not be reached.',
    );
    expect(screen.getByTestId('response-state').textContent).toBe(records.response.None);
    expect(screen.getByRole('button', { name: app.acknowledge })).toBeTruthy();
  });

  it('shows an error when the inbox cannot be loaded', async () => {
    mockApi({});

    renderAt(<InboxPage />);

    expect((await screen.findByRole('alert')).textContent).toContain('Unexpected request');
  });

  it('a text message is answered by keying 1 and pressing Reply', async () => {
    const api = mockApi({
      'GET /api/inbox': [inboxMessage({ channel: 'Sms' })],
      'POST /api/inbox/message-1/reply': inboxMessage({ channel: 'Sms', response: 'Acknowledged' }),
    });
    renderAt(<InboxPage />);
    const phone = await screen.findByRole('group', { name: en.inbox.channel.Sms });
    const reply = within(phone).getByRole('button', {
      name: en.inbox.sms.reply,
    }) as HTMLButtonElement;
    expect(reply.disabled).toBe(true);

    fireEvent.click(within(phone).getByRole('button', { name: 'Key 1' }));
    expect(screen.getByTestId('sms-draft').textContent).toBe('Reply: 1');
    fireEvent.click(reply);

    await waitFor(() =>
      expect(screen.getByTestId('response-state').textContent).toBe(records.response.Acknowledged),
    );
    expect(api.callsTo('POST /api/inbox/message-1/reply')[0]?.body).toEqual({ text: '1' });
    expect(screen.getByTestId('sms-draft').textContent).toBe('');
    expect(screen.queryByRole('button', { name: app.acknowledge })).toBeNull();
  });

  it('Back clears a mistyped reply before anything is sent', async () => {
    const api = mockApi({ 'GET /api/inbox': [inboxMessage({ channel: 'Sms' })] });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Key 7' }));
    fireEvent.click(screen.getByRole('button', { name: en.inbox.sms.back }));

    expect(screen.getByTestId('sms-draft').textContent).toBe('');
    expect(api.calls.every((call) => call.method === 'GET')).toBe(true);
  });

  it('a refused text reply shows why and leaves the message as it was', async () => {
    mockApi({
      'GET /api/inbox': [inboxMessage({ channel: 'Sms' })],
      'POST /api/inbox/message-1/reply': problem(
        400,
        'The reply was not understood.',
        'Reply 1 to acknowledge or 2 to ask for a call.',
      ),
    });
    renderAt(<InboxPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Key 3' }));
    fireEvent.click(screen.getByRole('button', { name: en.inbox.sms.reply }));

    expect((await screen.findByRole('alert')).textContent).toContain(
      'The reply was not understood.',
    );
    expect(screen.getByTestId('response-state').textContent).toBe(records.response.None);
  });

  it('a text that never reached the patient cannot be answered', async () => {
    mockApi({ 'GET /api/inbox': [inboxMessage({ channel: 'Sms', delivery: 'Pending' })] });

    renderAt(<InboxPage />);

    const phone = await screen.findByRole('group', { name: en.inbox.channel.Sms });
    for (const key of within(phone).getAllByRole('button')) {
      expect((key as HTMLButtonElement).disabled).toBe(true);
    }
    expect(screen.getByText(en.inbox.notAnswerable)).toBeTruthy();
  });

  it('a caregiver email is read-only', async () => {
    const api = mockApi({
      'GET /api/inbox': [
        inboxMessage({
          messageId: 'caregiver-message-1',
          channel: 'Email',
          recipientKind: 'Caregiver',
          patientSyntheticId: 'SYN-CG-INBOX-0001',
          patientDisplayName: 'Synthetic caregiver SYN-CG-INBOX-0001',
        }),
      ],
    });

    renderAt(<InboxPage />);

    const email = await screen.findByRole('group', { name: en.inbox.channel.Email });
    expect(
      screen.getByRole('tab', { name: 'Caregiver email (1)' }).getAttribute('aria-selected'),
    ).toBe('true');
    expect(within(email).getByText('Synthetic caregiver SYN-CG-INBOX-0001')).toBeTruthy();
    expect(screen.getByText(en.inbox.email.readOnly)).toBeTruthy();
    expect(within(email).queryByRole('button')).toBeNull();
    expect(within(email).queryByRole('textbox')).toBeNull();
    expect(within(email).queryByRole('link')).toBeNull();
    expect(screen.queryByTestId('response-state')).toBeNull();
    expect(screen.queryByTestId('confidence-state')).toBeNull();
    expect(screen.getByTestId('caregiver-note').textContent).toBe(records.caregiver);
    expect(api.calls.every((call) => call.method === 'GET')).toBe(true);
  });
});
