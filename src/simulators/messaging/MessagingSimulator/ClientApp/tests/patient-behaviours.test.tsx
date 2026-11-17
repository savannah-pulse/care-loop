import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  PatientBehavioursPage,
  fromDotNetDuration,
  toDotNetDuration,
} from '../src/features/patient-behaviours';
import en from '../src/locales/en.json';

import { behaviour, mockApi, problem, renderAt } from './helpers';

const fields = en.patientBehaviours.fields;

function assignment(patientSyntheticId: string, scenarioName: string | null = null) {
  return {
    patientSyntheticId,
    behaviour: behaviour({ response: 'Acknowledge', respondAfter: '00:00:02' }),
    scenarioName,
    simulated: true,
  };
}

/** The "set a patient's behaviour" form. */
async function patientForm() {
  const panel = (await screen.findByRole('heading', { name: en.patientBehaviours.addHeading }))
    .parentElement as HTMLElement;
  return within(panel);
}

describe('Patient behaviours', () => {
  it('lists the patients that have a behaviour of their own', async () => {
    mockApi({
      'GET /api/patient-behaviours': [
        assignment('SYN-PT-0001', 'happy-path'),
        assignment('SYN-PT-0002'),
      ],
      'GET /api/patient-behaviours/default': behaviour(),
    });

    renderAt(<PatientBehavioursPage />);

    const first = (await screen.findByRole('rowheader', { name: /SYN-PT-0001/ })).closest('tr');
    expect(first?.textContent).toContain('Delivered');
    expect(first?.textContent).toContain(en.patientBehaviours.response.Acknowledge);
    expect(first?.textContent).toContain('After 2 s, on notification 1');
    expect(first?.textContent).toContain('Scenario happy-path');
    expect(
      screen.getByRole('rowheader', { name: /SYN-PT-0002/ }).closest('tr')?.textContent,
    ).toContain(en.patientBehaviours.setByHand);
  });

  it('shows the default behaviour used for every other patient', async () => {
    const api = mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour({ delivery: 'StaySilent' }),
      'PUT /api/patient-behaviours/default': (call: { body: unknown }) => call.body,
    });
    renderAt(<PatientBehavioursPage />);

    const panel = within(
      (await screen.findByRole('heading', { name: en.patientBehaviours.defaultHeading }))
        .parentElement as HTMLElement,
    );
    const delivery = (await panel.findByLabelText(fields.delivery)) as HTMLSelectElement;
    expect(delivery.value).toBe('StaySilent');
    expect(screen.getByText(en.patientBehaviours.listEmpty)).toBeTruthy();

    fireEvent.change(delivery, { target: { value: 'Deliver' } });
    fireEvent.click(panel.getByRole('button', { name: en.patientBehaviours.saveDefault }));

    expect(await panel.findByRole('status')).toBeTruthy();
    expect(api.callsTo('PUT /api/patient-behaviours/default')[0]?.body).toEqual(behaviour());
  });

  it('saves a delivery outcome and a response for one patient', async () => {
    const onChanged = vi.fn();
    const api = mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
      'PUT /api/patient-behaviours/SYN-PT-0007': (call: { body: unknown }) => ({
        patientSyntheticId: 'SYN-PT-0007',
        behaviour: call.body,
        scenarioName: null,
        simulated: true,
      }),
    });
    renderAt(<PatientBehavioursPage onChanged={onChanged} />);
    const form = await patientForm();

    fireEvent.change(form.getByLabelText(en.patientBehaviours.patientIdLabel), {
      target: { value: 'SYN-PT-0007' },
    });
    fireEvent.change(form.getByLabelText(fields.response), { target: { value: 'CallMe' } });
    fireEvent.change(form.getByLabelText(fields.respondAfter), { target: { value: '90' } });
    fireEvent.change(form.getByLabelText(fields.respondToNotification), { target: { value: '2' } });
    fireEvent.click(form.getByRole('button', { name: en.patientBehaviours.save }));

    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    const sent = behaviour({
      response: 'CallMe',
      respondAfter: '00:01:30',
      respondToNotification: 2,
    });
    expect(api.callsTo('PUT /api/patient-behaviours/SYN-PT-0007')[0]?.body).toEqual(sent);
    expect(onChanged.mock.calls[0]?.[0].behaviour).toEqual(sent);
    expect(api.callsTo('GET /api/patient-behaviours')).toHaveLength(2);
  });

  it('requires a reason when delivery is set to fail', async () => {
    const api = mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
      'PUT /api/patient-behaviours/SYN-PT-0007': (call: { body: unknown }) => ({
        patientSyntheticId: 'SYN-PT-0007',
        behaviour: call.body,
        scenarioName: null,
        simulated: true,
      }),
    });
    renderAt(<PatientBehavioursPage />);
    const form = await patientForm();
    fireEvent.change(form.getByLabelText(en.patientBehaviours.patientIdLabel), {
      target: { value: 'SYN-PT-0007' },
    });
    fireEvent.change(form.getByLabelText(fields.delivery), { target: { value: 'Fail' } });

    fireEvent.click(form.getByRole('button', { name: en.patientBehaviours.save }));
    expect(form.getByRole('alert').textContent).toBe(en.patientBehaviours.validation.failureReason);
    expect(api.calls.some((call) => call.method === 'PUT')).toBe(false);

    fireEvent.change(form.getByLabelText(fields.failureReason), {
      target: { value: 'SIMULATED: unreachable' },
    });
    fireEvent.click(form.getByRole('button', { name: en.patientBehaviours.save }));

    await waitFor(() =>
      expect(api.callsTo('PUT /api/patient-behaviours/SYN-PT-0007')[0]?.body).toEqual(
        behaviour({ delivery: 'Fail', failureReason: 'SIMULATED: unreachable' }),
      ),
    );
  });

  it('allows a response only when delivery is set to deliver', async () => {
    const api = mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
      'PUT /api/patient-behaviours/SYN-PT-0007': (call: { body: unknown }) => ({
        patientSyntheticId: 'SYN-PT-0007',
        behaviour: call.body,
        scenarioName: null,
        simulated: true,
      }),
    });
    renderAt(<PatientBehavioursPage />);
    const form = await patientForm();
    fireEvent.change(form.getByLabelText(en.patientBehaviours.patientIdLabel), {
      target: { value: 'SYN-PT-0007' },
    });
    fireEvent.change(form.getByLabelText(fields.response), { target: { value: 'Acknowledge' } });

    fireEvent.change(form.getByLabelText(fields.delivery), { target: { value: 'StaySilent' } });

    const response = form.getByLabelText(fields.response) as HTMLSelectElement;
    expect(response.disabled).toBe(true);
    expect(response.value).toBe('None');
    expect(form.getByText(en.patientBehaviours.responseNeedsDelivery)).toBeTruthy();

    fireEvent.click(form.getByRole('button', { name: en.patientBehaviours.save }));
    await waitFor(() =>
      expect(api.callsTo('PUT /api/patient-behaviours/SYN-PT-0007')[0]?.body).toEqual(
        behaviour({ delivery: 'StaySilent' }),
      ),
    );
  });

  it('refuses a patient identifier that is not synthetic, and offers no teach-back or rating response', async () => {
    const api = mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
    });
    renderAt(<PatientBehavioursPage />);
    const form = await patientForm();

    fireEvent.change(form.getByLabelText(en.patientBehaviours.patientIdLabel), {
      target: { value: 'PT-0007' },
    });
    fireEvent.click(form.getByRole('button', { name: en.patientBehaviours.save }));
    expect(form.getByRole('alert').textContent).toBe(en.patientBehaviours.validation.patientId);

    fireEvent.change(form.getByLabelText(en.patientBehaviours.patientIdLabel), {
      target: { value: 'SYN-PT-0007' },
    });
    const offered = Array.from(
      (form.getByLabelText(fields.response) as HTMLSelectElement).options,
    ).map((option) => option.value);
    expect(offered).toEqual(['None', 'Acknowledge', 'CallMe']);
    expect(form.queryByLabelText(/confidence/i)).toBeNull();

    expect(api.calls.some((call) => call.method === 'PUT')).toBe(false);
  });

  it('shows the reason when the host refuses a behaviour', async () => {
    mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
      'PUT /api/patient-behaviours/SYN-PT-0007': problem(
        400,
        'The patient behaviour is not allowed.',
        'SIMULATED refusal',
      ),
    });
    renderAt(<PatientBehavioursPage />);
    const form = await patientForm();
    fireEvent.change(form.getByLabelText(en.patientBehaviours.patientIdLabel), {
      target: { value: 'SYN-PT-0007' },
    });

    fireEvent.click(form.getByRole('button', { name: en.patientBehaviours.save }));

    expect((await form.findByRole('alert')).textContent).toContain('SIMULATED refusal');
  });

  it('removes a behaviour so the default applies again', async () => {
    let removed = false;
    const api = mockApi({
      'GET /api/patient-behaviours': () => (removed ? [] : [assignment('SYN-PT-0001')]),
      'GET /api/patient-behaviours/default': behaviour(),
      'DELETE /api/patient-behaviours/SYN-PT-0001': () => {
        removed = true;
        return undefined;
      },
    });
    renderAt(<PatientBehavioursPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Remove behaviour of SYN-PT-0001' }));

    expect(await screen.findByText(en.patientBehaviours.listEmpty)).toBeTruthy();
    expect(api.callsTo('DELETE /api/patient-behaviours/SYN-PT-0001')).toHaveLength(1);
  });

  it('states that delivery is not an acknowledgement', async () => {
    mockApi({
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
    });

    renderAt(<PatientBehavioursPage />);

    expect(screen.getByText(/delivery is never an acknowledgement/)).toBeTruthy();
    await screen.findByText(en.patientBehaviours.listEmpty);
  });

  it('converts between seconds and the host duration format', () => {
    expect(toDotNetDuration(0)).toBe('00:00:00');
    expect(toDotNetDuration(2)).toBe('00:00:02');
    expect(toDotNetDuration(3661.5)).toBe('01:01:01.500');
    expect(toDotNetDuration(90000)).toBe('1.01:00:00');
    expect(fromDotNetDuration('00:00:02')).toBe(2);
    expect(fromDotNetDuration('1.01:00:00')).toBe(90000);
    expect(fromDotNetDuration('00:00:01.5000000')).toBe(1.5);
    expect(fromDotNetDuration('soon')).toBe(0);
  });
});
