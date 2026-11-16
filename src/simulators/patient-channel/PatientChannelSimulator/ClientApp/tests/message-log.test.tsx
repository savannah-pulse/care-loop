import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MessageLogPage } from '../src/features/message-log';
import en from '../src/locales/en.json';

import { logEntry, mockApi, renderAt } from './helpers';

const ENTRIES = [
  logEntry({
    entryId: 'newest',
    at: '2026-03-02T09:30:01+00:00',
    direction: 'Outbound',
    url: 'http://careloop.invalid/notifications/inbound/sms',
    kind: 'delivery',
    messageId: 'message-1',
    statusCode: 200,
    body: '{"type":"delivery","messageId":"message-1","status":"delivered"}',
  }),
  logEntry({ entryId: 'oldest' }),
  logEntry({
    entryId: 'unanswered',
    direction: 'Outbound',
    url: 'http://careloop.invalid/notifications/inbound/email',
    kind: 'reply',
    messageId: 'message-2',
    statusCode: null,
    body: null,
  }),
];

describe('Message log', () => {
  it('lists inbound and outbound exchanges newest first', async () => {
    mockApi({ 'GET /api/message-log': ENTRIES });

    renderAt(<MessageLogPage />);

    const rows = (await screen.findAllByRole('row')).slice(1);
    expect(rows).toHaveLength(3);
    expect(rows[0]?.textContent).toContain(en.messageLog.direction.Outbound);
    expect(rows[0]?.textContent).toContain(`${en.messageLog.kind.delivery} message-1`);
    expect(rows[0]?.textContent).toContain('200');
    expect(rows[1]?.textContent).toContain(en.messageLog.direction.Inbound);
    expect(rows[1]?.textContent).toContain('POST /messages');
    expect(rows[1]?.textContent).toContain(en.messageLog.kind.message);
    expect(rows[2]?.textContent).toContain(en.messageLog.noResponse);
  });

  it('shows the JSON of a selected exchange', async () => {
    mockApi({ 'GET /api/message-log': ENTRIES });
    renderAt(<MessageLogPage />);
    expect(await screen.findByText(en.messageLog.selectHint)).toBeTruthy();

    fireEvent.click(
      screen.getAllByRole('button', { name: /^Show Delivery report POST/ })[0] as HTMLElement,
    );

    expect(screen.getByTestId('exchange-json').textContent).toBe(
      '{\n  "type": "delivery",\n  "messageId": "message-1",\n  "status": "delivered"\n}',
    );

    fireEvent.click(screen.getByRole('button', { name: /^Show Reply POST/ }));
    expect(screen.getByText(en.messageLog.noBody)).toBeTruthy();
    expect(screen.queryByTestId('exchange-json')).toBeNull();
  });

  it('marks every entry as simulated', async () => {
    mockApi({ 'GET /api/message-log': ENTRIES });

    renderAt(<MessageLogPage />);

    const rows = (await screen.findAllByRole('row')).slice(1);
    for (const row of rows) {
      expect(row.textContent).toContain(en.common.simulated);
    }
  });

  it('limits the list and refreshes on request', async () => {
    const api = mockApi({ 'GET /api/message-log': ENTRIES });
    renderAt(<MessageLogPage maxEntries={2} />);
    expect((await screen.findAllByRole('row')).slice(1)).toHaveLength(2);

    fireEvent.click(screen.getByRole('button', { name: en.common.refresh }));

    await screen.findAllByRole('row');
    expect(api.callsTo('GET /api/message-log').length).toBeGreaterThanOrEqual(2);
  });

  it('says so when nothing has been exchanged', async () => {
    mockApi({ 'GET /api/message-log': [] });

    renderAt(<MessageLogPage />);

    expect(await screen.findByText(en.messageLog.empty)).toBeTruthy();
  });
});
