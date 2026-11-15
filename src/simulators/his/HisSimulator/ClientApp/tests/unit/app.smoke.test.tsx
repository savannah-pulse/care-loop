import { afterEach, describe, expect, it } from 'vitest';

import { App } from '../../src/App';
import en from '../../src/locales/en.json';
import { renderAt } from './render';
import type { RenderResult } from './render';

describe('App (smoke)', () => {
  let rendered: RenderResult | undefined;

  afterEach(() => {
    rendered?.unmount();
    rendered = undefined;
  });

  it('shows the simulated-data banner on every screen', () => {
    for (const route of ['/scenarios', '/inbox', '/fhir-log', '/actions']) {
      rendered = renderAt(<App />, route);
      const banner = rendered.container.querySelector('[data-testid="simulated-banner"]');
      expect(banner?.textContent).toBe(en.banner.simulated);
      rendered.unmount();
      rendered = undefined;
    }
  });

  it('redirects the root route to the scenario runner', () => {
    rendered = renderAt(<App />, '/');
    expect(rendered.container.querySelector('h2')?.textContent).toBe(en.scenarioRunner.title);
  });

  it('renders each screen with its translated title', () => {
    const screens: [string, string][] = [
      ['/scenarios', en.scenarioRunner.title],
      ['/inbox', en.staffInbox.title],
      ['/fhir-log', en.fhirLog.title],
      ['/actions', en.clinicianActions.title],
    ];
    for (const [route, title] of screens) {
      rendered = renderAt(<App />, route);
      expect(rendered.container.querySelector('h2')?.textContent).toBe(title);
      rendered.unmount();
      rendered = undefined;
    }
  });

  it('sets the document title from the locale file', () => {
    rendered = renderAt(<App />, '/scenarios');
    expect(document.title).toBe(en.app.title);
  });
});
