import { describe, expect, it, vi } from 'vitest';

import {
  ApiError,
  actOnInboxMessage,
  getCaseStatus,
  listInbox,
  listPatientBehaviours,
  rateConfidence,
  removePatientBehaviour,
  replyBySms,
  runScenario,
  setPatientBehaviour,
} from '../src/api';
import en from '../src/locales/en.json';

import { behaviour, inboxMessage, mockApi, problem, scenarioRun } from './helpers';

describe('API client', () => {
  it('listInbox fetches GET /api/inbox', async () => {
    const api = mockApi({ 'GET /api/inbox': [inboxMessage()] });

    const messages = await listInbox();

    expect(messages).toEqual([inboxMessage()]);
    expect(api.calls).toEqual([{ method: 'GET', path: '/api/inbox', body: undefined }]);
  });

  it('actOnInboxMessage posts to the action route', async () => {
    const api = mockApi({
      'POST /api/inbox/message%2F1/call-me': inboxMessage({ response: 'CallRequested' }),
    });

    const updated = await actOnInboxMessage('message/1', 'call-me');

    expect(updated.response).toBe('CallRequested');
    expect(api.calls[0]).toEqual({
      method: 'POST',
      path: '/api/inbox/message%2F1/call-me',
      body: undefined,
    });
  });

  it('rateConfidence posts the rating to the rate-confidence route', async () => {
    const api = mockApi({
      'POST /api/inbox/message-1/rate-confidence': inboxMessage({ confidence: 4 }),
    });

    expect((await rateConfidence('message-1', 4)).confidence).toBe(4);
    expect(api.calls[0]?.body).toEqual({ rating: 4 });
  });

  it('replyBySms posts the text to the reply route', async () => {
    const api = mockApi({
      'POST /api/inbox/message-1/reply': inboxMessage({ response: 'Acknowledged' }),
    });

    await replyBySms('message-1', '1');

    expect(api.calls[0]?.body).toEqual({ text: '1' });
  });

  it('getCaseStatus fetches GET /api/case-status/{taskId}', async () => {
    const status = {
      taskId: 'case-1',
      taskStatus: 'in-progress',
      stage: 'follow-up-active',
      statusReason: null,
      dueBy: null,
      lastUpdated: null,
      simulated: true,
    };
    const api = mockApi({ 'GET /api/case-status/case-1': status });

    expect(await getCaseStatus('case-1')).toEqual(status);
    expect(api.calls).toHaveLength(1);
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
      'POST /api/inbox/message-1/acknowledge': problem(
        502,
        'CareLoop could not be reached.',
        'SIMULATED: connection refused',
      ),
      'GET /api/inbox': () => {
        throw new TypeError('network down');
      },
    });

    const refused = await actOnInboxMessage('message-1', 'acknowledge').catch((e: unknown) => e);
    expect(refused).toBeInstanceOf(ApiError);
    expect((refused as ApiError).status).toBe(502);
    expect((refused as ApiError).message).toBe(
      'CareLoop could not be reached. SIMULATED: connection refused',
    );

    const unreachable = await listInbox().catch((e: unknown) => e);
    expect((unreachable as ApiError).status).toBe(0);
    expect((unreachable as ApiError).message).toBe(en.errors.unreachable);
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
