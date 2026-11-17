import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

import type {
  InboxMessage,
  MessageLogEntry,
  PatientBehaviour,
  PatientReactionRecord,
  ScenarioRunState,
} from '../src/api';

/** One request the screen made to the simulator's host. */
export interface ApiCall {
  method: string;
  path: string;
  body: unknown;
}

/** A non-2xx answer with a problem body, as the host sends it. */
export interface Problem {
  problemStatus: number;
  title: string;
  detail: string;
}

export function problem(problemStatus: number, title: string, detail: string): Problem {
  return { problemStatus, title, detail };
}

type Answer = unknown | ((call: ApiCall) => unknown);

function isProblem(value: unknown): value is Problem {
  return typeof value === 'object' && value !== null && 'problemStatus' in value;
}

/**
 * Replaces `fetch` with a fake host. Keys are `METHOD /path`; a value is the JSON to answer with,
 * a {@link problem}, `undefined` for 204, or a function of the call returning one of those.
 * A route that is not listed answers 500, so a test never passes on a request it did not expect.
 */
export function mockApi(routes: Record<string, Answer>) {
  const calls: ApiCall[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const call: ApiCall = {
      method: init?.method ?? 'GET',
      path: String(input),
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    };
    calls.push(call);

    const key = `${call.method} ${call.path}`;
    if (!(key in routes)) {
      return new Response(JSON.stringify({ title: `Unexpected request ${key}` }), { status: 500 });
    }

    const route = routes[key];
    const answer = typeof route === 'function' ? (route as (c: ApiCall) => unknown)(call) : route;
    if (isProblem(answer)) {
      return new Response(JSON.stringify({ title: answer.title, detail: answer.detail }), {
        status: answer.problemStatus,
        headers: { 'Content-Type': 'application/problem+json' },
      });
    }

    return answer === undefined
      ? new Response(null, { status: 204 })
      : new Response(JSON.stringify(answer), { status: 200 });
  });
  vi.stubGlobal('fetch', fetchMock);

  return {
    calls,
    /** The calls made with a given method and path. */
    callsTo: (key: string) => calls.filter((call) => `${call.method} ${call.path}` === key),
  };
}

export function renderAt(element: ReactElement, route = '/') {
  return render(<MemoryRouter initialEntries={[route]}>{element}</MemoryRouter>);
}

export function inboxMessage(overrides: Partial<InboxMessage> = {}): InboxMessage {
  return {
    messageId: 'message-1',
    caseId: 'case-1',
    to: 'SYN-PT-0001',
    displayName: 'Synthetic patient SYN-PT-0001',
    channel: 'Sms',
    subject: null,
    language: 'en',
    body: 'SIMULATED: a follow-up step is waiting. Reply 1 to confirm you got this message. Reply 2 for a call.',
    receivedAt: '2026-03-02T09:30:00+00:00',
    delivery: 'Delivered',
    deliveryFailureReason: null,
    recipientKind: 'Patient',
    replies: [],
    simulated: true,
    ...overrides,
  };
}

/** A patient's email, with a subject line. */
export function emailMessage(overrides: Partial<InboxMessage> = {}): InboxMessage {
  return inboxMessage({
    channel: 'Email',
    to: 'SYN-EMAIL-0001',
    displayName: 'Synthetic patient SYN-EMAIL-0001',
    subject: 'A follow-up step is waiting for you',
    ...overrides,
  });
}

export function behaviour(overrides: Partial<PatientBehaviour> = {}): PatientBehaviour {
  return {
    delivery: 'Deliver',
    response: 'None',
    failureReason: null,
    respondAfter: '00:00:00',
    respondToNotification: 1,
    simulated: true,
    ...overrides,
  };
}

export function reaction(overrides: Partial<PatientReactionRecord> = {}): PatientReactionRecord {
  return {
    messageId: 'message-1',
    patientSyntheticId: 'SYN-PT-0001',
    reaction: 'Delivered',
    automatic: true,
    sourceEventId: 'message-1/delivery/delivered',
    at: '2026-03-02T09:30:00+00:00',
    httpStatusCode: 200,
    succeeded: true,
    error: null,
    ...overrides,
  };
}

export function scenarioRun(overrides: Partial<ScenarioRunState> = {}): ScenarioRunState {
  return {
    runId: 'run-1',
    scenarioName: 'happy-path',
    status: 'Running',
    startedAt: '2026-03-02T09:00:00+00:00',
    completedAt: null,
    patientSyntheticIds: ['SYN-PT-0001'],
    reactions: [],
    simulated: true,
    ...overrides,
  };
}

export function logEntry(overrides: Partial<MessageLogEntry> = {}): MessageLogEntry {
  return {
    entryId: 'entry-1',
    at: '2026-03-02T09:30:00+00:00',
    direction: 'Inbound',
    method: 'POST',
    url: '/messages',
    kind: 'message',
    messageId: 'message-1',
    statusCode: 201,
    body: '{"messageId":"message-1","channel":"sms","to":"SYN-PT-0001","body":"SIMULATED"}',
    simulated: true,
    ...overrides,
  };
}
