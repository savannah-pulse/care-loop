import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CaseStatusPage } from '../src/features/case-status';
import en from '../src/locales/en.json';

import { mockApi, problem, renderAt } from './helpers';

const STATUS = {
  taskId: 'case-1',
  taskStatus: 'in-progress',
  stage: 'follow-up-active',
  statusReason: null,
  dueBy: '2026-03-04T09:00:00+00:00',
  lastUpdated: '2026-03-02T09:00:00+00:00',
  simulated: true,
};

describe('Patient case status', () => {
  it('looks up a case by Task id', async () => {
    const api = mockApi({ 'GET /api/case-status/case-1': STATUS });
    renderAt(<CaseStatusPage />);
    expect(api.calls).toHaveLength(0);

    fireEvent.change(screen.getByLabelText(en.caseStatus.taskIdLabel), {
      target: { value: ' case-1 ' },
    });
    fireEvent.click(screen.getByRole('button', { name: en.caseStatus.lookUp }));

    expect((await screen.findByTestId('case-taskId')).textContent).toBe('case-1');
    expect(api.callsTo('GET /api/case-status/case-1')).toHaveLength(1);
  });

  it('opens the case named in the link from the inbox', async () => {
    mockApi({ 'GET /api/case-status/case-1': STATUS });

    renderAt(<CaseStatusPage />, '/case-status?taskId=case-1');

    expect((await screen.findByTestId('case-stage')).textContent).toBe('follow-up-active');
  });

  it('shows stage, deadline and last update', async () => {
    mockApi({ 'GET /api/case-status/case-1': STATUS });

    renderAt(<CaseStatusPage taskId="case-1" />);

    expect((await screen.findByTestId('case-stage')).textContent).toBe('follow-up-active');
    expect(screen.getByTestId('case-taskStatus').textContent).toBe('in-progress');
    expect(screen.getByTestId('case-dueBy').textContent).toContain('2026');
    expect(screen.getByTestId('case-lastUpdated').textContent).toContain('2026');
    expect(screen.getByTestId('case-statusReason').textContent).toBe(en.caseStatus.notStated);
    expect(screen.getByText(en.common.simulated)).toBeTruthy();
  });

  it('never shows clinical detail', async () => {
    // Even if the host were to send more than the patient view allows, only the listed fields are shown.
    mockApi({
      'GET /api/case-status/case-1': {
        ...STATUS,
        owner: 'Synthetic Coordinator',
        urgency: 'PLACEHOLDER-URGENCY-A',
        result: 'PLACEHOLDER-RESULT-ACTIONABLE',
      },
    });

    const { container } = renderAt(<CaseStatusPage taskId="case-1" />);
    await screen.findByTestId('case-stage');

    expect(container.textContent).not.toContain('Synthetic Coordinator');
    expect(container.textContent).not.toContain('PLACEHOLDER-URGENCY-A');
    expect(container.textContent).not.toContain('PLACEHOLDER-RESULT-ACTIONABLE');
    expect(container.querySelectorAll('dt')).toHaveLength(6);
  });

  it('shows a not-found message for an unknown Task', async () => {
    mockApi({ 'GET /api/case-status/unknown': problem(404, '', '') });

    renderAt(<CaseStatusPage taskId="unknown" />);

    expect((await screen.findByRole('alert')).textContent).toBe(en.caseStatus.notFound);
    expect(screen.queryByTestId('case-stage')).toBeNull();
  });

  it('shows why when CareLoop cannot be reached', async () => {
    mockApi({
      'GET /api/case-status/case-1': problem(502, 'CareLoop could not be reached.', 'SIMULATED'),
    });

    renderAt(<CaseStatusPage taskId="case-1" />);

    expect((await screen.findByRole('alert')).textContent).toContain(
      'CareLoop could not be reached.',
    );
  });
});
