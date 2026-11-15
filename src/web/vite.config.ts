import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv, type PluginOption, type ProxyOptions } from 'vite'

/** Where `/api` is forwarded when `CARELOOP_API_URL` is not set: the API started with `dotnet run`. */
const DEFAULT_API_URL = 'http://localhost:5080'

/**
 * Vite configuration for the CareLoop dashboard.
 *
 * @remarks
 * The app calls the CareLoop API at `/api` on its own origin. The dev server and `vite preview`
 * forward `/api`, without the prefix, to `CARELOOP_API_URL`: `http://localhost:5080` by default,
 * where `dotnet run` starts the API; Docker Compose publishes it on `http://localhost:8080`. With
 * `CARELOOP_MOCK_API=1` they answer `/api` from the local mock API in `mock/` instead, which is
 * loaded only then and never reaches the built app; `CARELOOP_MOCK_NOW` pins the moment its
 * fixtures describe. Settings are read from the environment or `.env` files.
 */
export default defineConfig(async ({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'CARELOOP_')
  const plugins: PluginOption[] = [react()]
  if (env.CARELOOP_MOCK_API === '1') {
    const { careLoopMockApi } = await import('./mock/plugin.ts')
    plugins.push(careLoopMockApi({ now: env.CARELOOP_MOCK_NOW }))
    return { plugins }
  }
  const proxy: Record<string, ProxyOptions> = {
    '/api': {
      target: env.CARELOOP_API_URL || DEFAULT_API_URL,
      changeOrigin: true,
      rewrite: (path) => path.replace(/^\/api/, ''),
    },
  }
  return { plugins, server: { proxy }, preview: { proxy } }
})
