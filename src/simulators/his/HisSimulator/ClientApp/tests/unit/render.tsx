import { act } from 'react';
import type { ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nextProvider } from 'react-i18next';
import { MemoryRouter } from 'react-router-dom';

import { i18n } from '../../src/i18n';

/** What {@link renderAt} returns. */
export interface RenderResult {
  /** The DOM element the tree was rendered into. */
  container: HTMLElement;
  /** Unmounts the tree and removes the container. */
  unmount: () => void;
}

/**
 * Renders a React tree inside the i18n provider and a memory router at the given route.
 *
 * @param ui - The tree to render.
 * @param route - The initial client-side route.
 * @returns The container and an unmount function.
 */
export function renderAt(ui: ReactNode, route = '/'): RenderResult {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);

  act(() => {
    root.render(
      <I18nextProvider i18n={i18n}>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </I18nextProvider>,
    );
  });

  return {
    container,
    unmount: () => {
      act(() => {
        root.unmount();
      });
      container.remove();
    },
  };
}
