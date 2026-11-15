import { act } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ClinicianActionsPage } from '../../src/features/clinician-actions';
import en from '../../src/locales/en.json';
import { fill, flush, get, renderAt } from './render';
import type { RenderResult } from './render';

const sourceCase = {
  caseRef: 'stable/c1',
  patient: { displayName: 'Synthetic Patient 1', syntheticId: 'SYN-PT-1' },
  resourceIds: { outcome: 'Procedure/SYN-EVIDENCE-1' },
  careLoopTaskId: 'case-task',
};
describe('ClinicianActionsPage', () => {
  let rendered: RenderResult;
  const fetchMock = vi.fn<typeof fetch>();
  beforeEach(() => {
    fetchMock
      .mockReset()
      .mockImplementation(
        async (_url, options) =>
          new Response(
            JSON.stringify(
              options?.method === 'POST'
                ? { accepted: true, messages: [{ resourceType: 'Task', statusCode: 200 }] }
                : { cases: [sourceCase] },
            ),
          ),
      );
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    rendered.unmount();
    vi.unstubAllGlobals();
  });
  async function ready(kind: string) {
    rendered = renderAt(<ClinicianActionsPage apiBaseUrl="https://synthetic-his.invalid" />);
    await flush();
    fill(rendered.container, 'caseRef', sourceCase.caseRef);
    fill(rendered.container, 'action', kind);
    fill(rendered.container, 'performer', 'SYN-STAFF-1');
    fill(rendered.container, 'role', 'clinician');
    fill(rendered.container, 'facility', 'SYN-FAC-A');
  }
  async function submit() {
    act(() => {
      get(rendered.container, 'form').dispatchEvent(
        new Event('submit', { bubbles: true, cancelable: true }),
      );
    });
    await flush();
    const call = fetchMock.mock.calls.find(([, options]) => options?.method === 'POST');
    expect(call).toBeDefined();
    if (call === undefined) throw new Error('No action submitted');
    return {
      url: String(call[0]),
      body: JSON.parse(String(call[1]?.body)) as Record<string, unknown>,
    };
  }
  it('lists synthetic cases and performs no action until a human submits', async () => {
    await ready('clinical-review');
    expect(rendered.container.textContent).toContain('Synthetic Patient 1');
    expect(fetchMock.mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true);
  });
  it('forwards the reviewed urgency unchanged and honors the API base URL', async () => {
    await ready('clinical-review');
    fill(rendered.container, 'urgency', 'PLACEHOLDER-URGENCY-A');
    const result = await submit();
    expect(result.url).toBe('https://synthetic-his.invalid/actions/clinical-review');
    expect(result.body.urgency).toBe('PLACEHOLDER-URGENCY-A');
    expect(result.body.performer).toEqual({
      syntheticId: 'SYN-STAFF-1',
      role: 'clinician',
      facility: 'SYN-FAC-A',
    });
  });
  it.each(['accepted', 'rejected'])(
    'forwards explicit referral decision %s and its source reason',
    async (status) => {
      await ready('referral-decision');
      fill(rendered.container, 'status', status);
      fill(rendered.container, 'reason', 'SIMULATED source reason');
      const result = await submit();
      expect(result.body.accepted).toBe(status === 'accepted');
      expect(result.body.reason).toBe('SIMULATED source reason');
    },
  );
  it('records finalized outcome facts, not an invented evidence kind', async () => {
    await ready('outcome');
    fill(rendered.container, 'evidenceKind', 'diagnosis');
    expect((await submit()).body).toMatchObject({
      evidenceKind: 'diagnosis',
      asFollowUpReport: false,
    });
  });
  it('submits the exact selected evidence and shows clinical refusal separately from a local HTTP failure', async () => {
    await ready('closure');
    fill(rendered.container, 'closureReason', 'Treated');
    fill(rendered.container, 'evidenceResourceKey', 'outcome');
    fetchMock.mockImplementation(
      async (_url, options) =>
        new Response(
          JSON.stringify(
            options?.method === 'POST'
              ? { accepted: false, messages: [{ resourceType: 'Task', statusCode: 422 }] }
              : { cases: [sourceCase] },
          ),
        ),
    );
    expect((await submit()).body).toMatchObject({
      closureReason: 'Treated',
      evidenceResourceKey: 'outcome',
    });
    expect(rendered.container.textContent).toContain(en.clinicianActions.refused);
    expect(rendered.container.textContent).toContain('422');
    expect(rendered.container.querySelector('[role="alert"]')).toBeNull();
  });
  it('shows local validation failures explicitly instead of success', async () => {
    await ready('clinical-review');
    fill(rendered.container, 'urgency', 'PLACEHOLDER-URGENCY-A');
    fetchMock.mockImplementation(async () => new Response('{}', { status: 400 }));
    await submit();
    expect(rendered.container.querySelector('[role="alert"]')?.textContent).toContain('400');
    expect(rendered.container.textContent).not.toContain(en.clinicianActions.accepted);
  });
  it('exposes consultation, appointment, consent and the extended staff roles', async () => {
    await ready('consent');
    fill(rendered.container, 'status', 'active');
    fill(rendered.container, 'caregiverContact', 'SYN-CG-1');
    expect((await submit()).body).toMatchObject({ status: 'active', caregiverContact: 'SYN-CG-1' });
    expect(rendered.container.textContent).toContain(en.clinicianActions.roles.lab);
    expect(rendered.container.textContent).toContain(en.clinicianActions.actions.appointment);
    expect(rendered.container.textContent).toContain(
      en.clinicianActions.actions['consultation-request'],
    );
  });
});
