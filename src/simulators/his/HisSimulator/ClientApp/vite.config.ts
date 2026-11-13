import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

/** Where the HIS simulator API listens during local development. */
const apiTarget = 'http://localhost:5081';

/**
 * API path prefixes proxied to the simulator in development.
 * The ClientApp uses hash routing, so these never collide with client-side routes.
 */
const apiPaths = [
  '/health',
  '/fhir',
  '/fhir-log',
  '/scenarios',
  '/staff-inbox',
  '/actions',
  '/cases',
];

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5181,
    proxy: Object.fromEntries(
      apiPaths.map((path) => [path, { target: apiTarget, changeOrigin: true }]),
    ),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    include: ['tests/unit/**/*.test.{ts,tsx}'],
    setupFiles: ['tests/setup.ts'],
  },
});
