import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

import '../src/i18n';

// Vitest runs without globals, so Testing Library cannot register its own cleanup.
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});
