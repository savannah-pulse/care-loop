import { describe, expect, it, vi } from 'vitest';

import {
  ApiError,
  listInbox,
  listMessageLog,
  listPatientBehaviours,
  removePatientBehaviour,
  replyToMessage,
  runScenario,
  setPatientBehaviour,
} from '../src/api';
import en from '../src/locales/en.json';

import { behaviour, inboxMessage, logEntry, mockApi, problem, scenarioRun } from './helpers';

describe('API client', () => {
  it('listInbox fetches GET /api/inbox', async () => {
    const api = mockApi({ 'GET /api/inbox': [inboxMessage()] });

    const messages = await listInbox();

    expect(messages).toEqual([inboxMessage()]);
    expect(api.calls).toEqual([{ method: 'GET', path: '/api/inbox', body: undefined }]);
  });

  it('replyToMessage posts the text as typed to the reply route', async () => {
    const replied = inboxMessage({
      replies: [
        {
          replyId: 'message/1-r1',
          text: ' can you call me? ',
          sentAt: '2026-03-02T09:31:00+00:00',
        },
      ],
    });
    const api = mockApi({ 'POST /api/inbox/message%2F1/reply': replied });

    const updated = await replyToMessage('message/1', ' can you call me? ');

    expect(updated.replies).toHaveLength(1);
    expect(api.calls[0]).toEqual({
      method: 'POST',
      path: '/api/inbox/message%2F1/reply',
      body: { text: ' can you call me? ' },
    });
  });

  it('listMessageLog fetches GET /api/message-log', async () => {
    const api = mockApi({ 'GET /api/message-log': [logEntry()] });

    expect(await listMessageLog()).toEqual([logEntry()]);
    expect(api.calls).toEqual([{ method: 'GET', path: '/api/message-log', body: undefined }]);
  });

  it('listPatientBehaviours fetches GET /api/patient-behaviours', async () => {
    mockApi({ 'GET /api/patient-behaviours': [] });

    expect(await listPatientBehaviours()).toEqual([]);
  });

  it('setPatientBehaviour sends PUT /api/patient-behaviours/{patientSyntheticId}', async () => {
    const stored = {
      patientSyntheticId: 'SYN-PT-0001',
      behaviour: behaviour({ response: 'Acknowledge' }),
      scenarioName: null,
      simulated: true,
    };
    const api = mockApi({ 'PUT /api/patient-behaviours/SYN-PT-0001': stored });

    expect(
      await setPatientBehaviour('SYN-PT-0001', behaviour({ response: 'Acknowledge' })),
    ).toEqual(stored);
    expect(api.calls[0]?.body).toEqual(behaviour({ response: 'Acknowledge' }));
  });

  it('removePatientBehaviour sends DELETE /api/patient-behaviours/{patientSyntheticId}', async () => {
    const api = mockApi({ 'DELETE /api/patient-behaviours/SYN-PT-0001': undefined });

    await expect(removePatientBehaviour('SYN-PT-0001')).resolves.toBeUndefined();
    expect(api.calls[0]?.method).toBe('DELETE');
  });

  it('runScenario posts to /scenarios/{name}/run', async () => {
    const api = mockApi({ 'POST /scenarios/happy-path/run': scenarioRun() });

    expect((await runScenario('happy-path')).runId).toBe('run-1');
    expect(api.calls[0]?.body).toEqual({});

    await runScenario('happy-path', 30);
    expect(api.calls[1]?.body).toEqual({ realSecondsPerScenarioHour: 30 });
  });

  it('rejects with a readable error on a non-2xx response', async () => {
    mockApi({
      'POST /api/inbox/message-1/reply': problem(
        502,
        'CareLoop could not be reached.',
        'SIMULATED: connection refused',
      ),
      'GET /api/inbox': () => {
        throw new TypeError('network down');
      },
    });

    const refused = await replyToMessage('message-1', '1').catch((e: unknown) => e);
    expect(refused).toBeInstanceOf(ApiError);
    expect((refused as ApiError).status).toBe(502);
    expect((refused as ApiError).message).toBe(
      'CareLoop could not be reached. SIMULATED: connection refused',
    );

    const unreachable = await listInbox().catch((e: unknown) => e);
    expect((unreachable as ApiError).status).toBe(0);
    expect((unreachable as ApiError).message).toBe(en.errors.unreachable);
  });

  it('rejects a successful answer that is not JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('<!doctype html><title>shell</title>', { status: 200 })),
    );

    const failure = await listInbox().catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).message).toBe(en.errors.notJson);
  });

  it('falls back to the status when the failure has no readable body', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('<html>gateway</html>', { status: 504 })),
    );

    const failure = await listInbox().catch((e: unknown) => e);

    expect((failure as ApiError).message).toBe('The request failed with status 504.');
  });
});
