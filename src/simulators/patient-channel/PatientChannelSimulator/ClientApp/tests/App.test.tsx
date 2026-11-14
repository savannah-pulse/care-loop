import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../src/App';
import en from '../src/locales/en.json';
import { ROUTES } from '../src/routes';

import { behaviour, mockApi } from './helpers';

describe('App shell', () => {
  beforeEach(() => {
    mockApi({
      'GET /api/inbox': [],
      'GET /api/patient-behaviours': [],
      'GET /api/patient-behaviours/default': behaviour(),
      'GET /scenarios': [],
      'GET /api/fhir-log': [],
    });
  });

  it('shows the simulated-data banner and the inbox on the landing route', async () => {
    render(
      <MemoryRouter initialEntries={[ROUTES.inbox]}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByTestId('simulated-banner').textContent).toBe(en.banner.simulated);
    expect(screen.getByRole('heading', { level: 2 }).textContent).toBe(en.inbox.title);
    expect(await screen.findByText(en.inbox.empty)).toBeTruthy();
  });

  it.each([
    [ROUTES.caseStatus, en.caseStatus.title],
    [ROUTES.patientBehaviours, en.patientBehaviours.title],
    [ROUTES.scenarioRunner, en.scenarioRunner.title],
    [ROUTES.messageLog, en.messageLog.title],
  ])('renders the screen at %s under one level-2 heading', async (path, title) => {
    render(
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { level: 2 }).textContent).toBe(title);
    expect(screen.getByTestId('simulated-banner')).toBeTruthy();
    await screen.findByRole('heading', { level: 2 });
  });

  it('keeps client routes clear of the paths served by the .NET host', () => {
    const reserved = ['/api', '/fhir', '/scenarios', '/health'];
    for (const path of Object.values(ROUTES)) {
      expect(reserved.some((prefix) => path.startsWith(prefix))).toBe(false);
    }
  });
});
