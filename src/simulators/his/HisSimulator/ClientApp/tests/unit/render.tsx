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

/**
 * Changes a form control through its native setter so React observes the input.
 * @param container - Rendered DOM.
 * @param name - Control name.
 * @param value - Source-authored value to enter.
 */
export function fill(container: HTMLElement, name: string, value: string) {
  const control = container.querySelector<
    HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement
  >(`[name="${name}"]`);
  if (control === null) throw new Error(`Missing control ${name}`);
  act(() => {
    const setter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(control), 'value')?.set;
    if (setter === undefined) throw new Error(`Missing value setter ${name}`);
    setter.call(control, value);
    control.dispatchEvent(new Event('input', { bubbles: true }));
    control.dispatchEvent(new Event('change', { bubbles: true }));
  });
}

/** Settles React effects and asynchronous mock responses. */
export async function flush() {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

/**
 * Requires a DOM element instead of hiding a missing control with a non-null assertion.
 * @param container - Rendered DOM.
 * @param selector - Selector for the required element.
 * @returns The matching element.
 */
export function get<T extends Element>(container: HTMLElement, selector: string): T {
  const element = container.querySelector<T>(selector);
  if (element === null) throw new Error(`Missing element ${selector}`);
  return element;
}
