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
    url: 'http://careloop.invalid/notifications/inbound/simulator',
    resourceType: 'Communication',
    resourceId: 'message-1-delivery',
    statusCode: 200,
    body: '{"resourceType":"Communication","id":"message-1-delivery"}',
  }),
  logEntry({ entryId: 'oldest' }),
  logEntry({
    entryId: 'read',
    direction: 'Outbound',
    method: 'GET',
    url: 'http://careloop.invalid/fhir/Task/case-1',
    resourceType: 'Task',
    resourceId: 'case-1',
    statusCode: null,
    body: null,
  }),
];

describe('FHIR message log', () => {
  it('lists inbound and outbound exchanges newest first', async () => {
    mockApi({ 'GET /api/fhir-log': ENTRIES });

    renderAt(<MessageLogPage />);

    const rows = (await screen.findAllByRole('row')).slice(1);
    expect(rows).toHaveLength(3);
    expect(rows[0]?.textContent).toContain(en.messageLog.direction.Outbound);
    expect(rows[0]?.textContent).toContain('Communication/message-1-delivery');
    expect(rows[0]?.textContent).toContain('200');
    expect(rows[1]?.textContent).toContain(en.messageLog.direction.Inbound);
    expect(rows[1]?.textContent).toContain('POST /fhir/CommunicationRequest');
    expect(rows[2]?.textContent).toContain(en.messageLog.noResponse);
  });

  it('shows the FHIR JSON of a selected exchange', async () => {
    mockApi({ 'GET /api/fhir-log': ENTRIES });
    renderAt(<MessageLogPage />);
    expect(await screen.findByText(en.messageLog.selectHint)).toBeTruthy();

    fireEvent.click(
      screen.getAllByRole('button', { name: /^Show Communication POST/ })[0] as HTMLElement,
    );

    expect(screen.getByTestId('fhir-json').textContent).toBe(
      '{\n  "resourceType": "Communication",\n  "id": "message-1-delivery"\n}',
    );

    fireEvent.click(screen.getByRole('button', { name: /^Show Task GET/ }));
    expect(screen.getByText(en.messageLog.noBody)).toBeTruthy();
    expect(screen.queryByTestId('fhir-json')).toBeNull();
  });

  it('marks every entry as simulated', async () => {
    mockApi({ 'GET /api/fhir-log': ENTRIES });

    renderAt(<MessageLogPage />);

    const rows = (await screen.findAllByRole('row')).slice(1);
    for (const row of rows) {
      expect(row.textContent).toContain(en.common.simulated);
    }
  });

  it('limits the list and refreshes on request', async () => {
    const api = mockApi({ 'GET /api/fhir-log': ENTRIES });
    renderAt(<MessageLogPage maxEntries={2} />);
    expect((await screen.findAllByRole('row')).slice(1)).toHaveLength(2);

    fireEvent.click(screen.getByRole('button', { name: en.common.refresh }));

    await screen.findAllByRole('row');
    expect(api.callsTo('GET /api/fhir-log').length).toBeGreaterThanOrEqual(2);
  });

  it('says so when nothing has been exchanged', async () => {
    mockApi({ 'GET /api/fhir-log': [] });

    renderAt(<MessageLogPage />);

    expect(await screen.findByText(en.messageLog.empty)).toBeTruthy();
  });
});
