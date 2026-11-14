import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

import type {
  FhirMessageLogEntry,
  InboxMessage,
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
    communicationRequestId: 'message-1',
    caseTaskId: 'case-1',
    patientSyntheticId: 'SYN-PT-0001',
    patientDisplayName: 'Synthetic patient SYN-PT-0001',
    channel: 'App',
    language: 'en',
    body: 'SIMULATED: please contact the clinic about your follow-up.',
    receivedAt: '2026-03-02T09:30:00+00:00',
    delivery: 'Delivered',
    deliveryFailureReason: null,
    response: 'None',
    respondedAt: null,
    simulated: true,
    recipientKind: 'Patient',
    confidence: null,
    confidenceRatedAt: null,
    callRequestCount: 0,
    confidenceRatingCount: 0,
    ...overrides,
  };
}

export function behaviour(overrides: Partial<PatientBehaviour> = {}): PatientBehaviour {
  return {
    delivery: 'Deliver',
    response: 'None',
    failureReason: null,
    respondAfter: '00:00:00',
    respondToNotification: 1,
    simulated: true,
    confidence: null,
    ...overrides,
  };
}

export function reaction(overrides: Partial<PatientReactionRecord> = {}): PatientReactionRecord {
  return {
    messageId: 'message-1',
    patientSyntheticId: 'SYN-PT-0001',
    reaction: 'Delivered',
    automatic: true,
    sourceEventId: 'Communication/message-1-delivery/_history/1',
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

export function logEntry(overrides: Partial<FhirMessageLogEntry> = {}): FhirMessageLogEntry {
  return {
    entryId: 'entry-1',
    at: '2026-03-02T09:30:00+00:00',
    direction: 'Inbound',
    method: 'POST',
    url: '/fhir/CommunicationRequest',
    resourceType: 'CommunicationRequest',
    resourceId: 'message-1',
    statusCode: 201,
    body: '{"resourceType":"CommunicationRequest","id":"message-1"}',
    simulated: true,
    ...overrides,
  };
}
