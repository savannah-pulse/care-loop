import { act } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ClinicianActionsPage } from '../../src/features/clinician-actions';
import en from '../../src/locales/en.json';
import { fill, flush, get, renderAt } from './render';
import type { RenderResult } from './render';

const sourceCase = {
  caseRef: 'stable/c1',
  patient: { displayName: 'Synthetic Patient 1', syntheticId: 'SYN-PT-1', mrn: 'SYN-MRN-PT-1' },
  resourceIds: { outcome: 'Procedure/SYN-EVIDENCE-1', 'referral-task': 'SYN-TASK-9' },
  careLoopTaskId: 'case-task',
  approvedHandoffSummary: null as string | null,
};
const closureReasons = [
  'treated',
  'on-surveillance',
  'negative-on-confirmation',
  'declined',
  'transferred-and-accepted',
  'untraceable',
];
describe('ClinicianActionsPage', () => {
  let rendered: RenderResult;
  const fetchMock = vi.fn<typeof fetch>();
  function serveCases(summary: string | null = null) {
    fetchMock
      .mockReset()
      .mockImplementation(
        async (_url, options) =>
          new Response(
            JSON.stringify(
              options?.method === 'POST'
                ? { accepted: true, messages: [{ resourceType: 'Task', statusCode: 200 }] }
                : { cases: [{ ...sourceCase, approvedHandoffSummary: summary }] },
            ),
          ),
      );
  }
  beforeEach(() => {
    serveCases();
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    rendered.unmount();
    vi.unstubAllGlobals();
  });
  function click(selector: string) {
    act(() => {
      get<HTMLInputElement>(rendered.container, selector).click();
    });
  }
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
      fill(rendered.container, 'reason', en.clinicianActions.declineReasons.noCapacity);
      const result = await submit();
      expect(result.body.accepted).toBe(status === 'accepted');
      expect(result.body.reason).toBe(en.clinicianActions.declineReasons.noCapacity);
    },
  );
  it('offers the fixed decline reasons as a select, not free text', async () => {
    await ready('referral-decision');
    const options = Array.from(
      get<HTMLSelectElement>(rendered.container, 'select[name="reason"]').options,
      (option) => option.value,
    );
    expect(options).toEqual(['', ...Object.values(en.clinicianActions.declineReasons)]);
    expect(rendered.container.querySelector('textarea[name="reason"]')).toBeNull();
  });
  it('does not offer a completed referral, which CareLoop never counts as care', async () => {
    await ready('referral');
    const options = Array.from(
      get<HTMLSelectElement>(rendered.container, 'select[name="status"]').options,
      (option) => option.value,
    );
    expect(options).toEqual(['', 'requested', 'accepted', 'rejected']);
  });
  it('offers the six closure reasons as radio buttons and sends the wire code', async () => {
    await ready('closure');
    const radios = Array.from(
      rendered.container.querySelectorAll<HTMLInputElement>('input[name="closureReason"]'),
    );
    expect(radios.map((radio) => radio.value)).toEqual(closureReasons);
    expect(radios.every((radio) => radio.type === 'radio' && radio.required)).toBe(true);
    click('input[name="closureReason"][value="negative-on-confirmation"]');
    click('input[name="evidenceChecked"]');
    expect((await submit()).body.closureReason).toBe('negative-on-confirmation');
  });
  it('sends the next surveillance date as the unchanged yyyy-MM-dd value', async () => {
    await ready('closure');
    click('input[name="closureReason"][value="on-surveillance"]');
    fill(rendered.container, 'nextSurveillanceDate', '2027-03-04');
    click('input[name="evidenceChecked"]');
    expect(
      get<HTMLInputElement>(rendered.container, 'input[name="nextSurveillanceDate"]').type,
    ).toBe('date');
    expect((await submit()).body).toMatchObject({
      closureReason: 'on-surveillance',
      nextSurveillanceDate: '2027-03-04',
    });
  });
  it('sends no closure until the evidence has been checked', async () => {
    await ready('closure');
    click('input[name="closureReason"][value="treated"]');
    const button = get<HTMLButtonElement>(rendered.container, 'form button');
    expect(button.disabled).toBe(true);
    act(() => {
      get(rendered.container, 'form').dispatchEvent(
        new Event('submit', { bubbles: true, cancelable: true }),
      );
    });
    await flush();
    expect(fetchMock.mock.calls.some(([, options]) => options?.method === 'POST')).toBe(false);
    click('input[name="evidenceChecked"]');
    expect(button.disabled).toBe(false);
  });
  it('offers only outcome resources as closure evidence', async () => {
    await ready('closure');
    const options = Array.from(
      get<HTMLSelectElement>(rendered.container, 'select[name="evidenceResourceKey"]').options,
      (option) => option.value,
    );
    expect(options).toEqual(['', 'outcome']);
  });
  it('requires a facility only when a proposed owner is entered', async () => {
    await ready('consultation-request');
    const facility = () =>
      get<HTMLInputElement>(rendered.container, 'input[name="receivingFacility"]');
    expect(facility().required).toBe(false);
    fill(rendered.container, 'proposedOwner', 'SYN-STAFF-2');
    expect(facility().required).toBe(true);
    fill(rendered.container, 'proposedRole', 'receiving-clinician');
    fill(rendered.container, 'receivingFacility', 'SYN-FAC-B');
    expect((await submit()).body.proposedOwner).toEqual({
      syntheticId: 'SYN-STAFF-2',
      role: 'receiving-clinician',
      facility: 'SYN-FAC-B',
    });
  });
  it('sends no proposed owner when none was entered', async () => {
    await ready('consultation-request');
    expect((await submit()).body).not.toHaveProperty('proposedOwner');
  });
  it('shows the patient name and MRN with the case reference', async () => {
    await ready('clinical-review');
    expect(rendered.container.textContent).toContain('Synthetic Patient 1');
    expect(rendered.container.textContent).toContain('SYN-MRN-PT-1');
    expect(rendered.container.textContent).toContain('stable/c1');
  });
  it('shows the approved handoff summary read-only only once one was received', async () => {
    await ready('clinical-review');
    expect(rendered.container.querySelector('[data-testid="handoff-summary"]')).toBeNull();
    rendered.unmount();
    serveCases('SIMULATED approved handoff text');
    await ready('clinical-review');
    const summary = get(rendered.container, '[data-testid="handoff-summary"]');
    expect(summary.textContent).toContain('SIMULATED approved handoff text');
    expect(summary.querySelector('input, textarea, button')).toBeNull();
  });
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
    click('input[name="closureReason"][value="treated"]');
    click('input[name="evidenceChecked"]');
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
      closureReason: 'treated',
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
