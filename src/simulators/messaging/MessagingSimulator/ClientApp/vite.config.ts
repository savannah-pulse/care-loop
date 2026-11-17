import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

/** Local address of the messaging simulator's .NET host during development. */
const SIMULATOR_URL = 'http://localhost:5082';

/** Path prefixes served by the .NET host; everything else is a client-side route. */
const PROXIED_PREFIXES = ['/api', '/messages', '/scenarios', '/health', '/openapi'];

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: Object.fromEntries(
      PROXIED_PREFIXES.map((prefix) => [prefix, { target: SIMULATOR_URL, changeOrigin: true }]),
    ),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    include: ['tests/**/*.test.{ts,tsx}'],
    setupFiles: ['tests/setup.ts'],
  },
});
