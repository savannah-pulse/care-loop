import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv, type PluginOption, type ProxyOptions } from 'vite'

/** Where `/api` is forwarded when the mock API is off and no other target is set. */
const DEFAULT_API_PROXY = 'http://localhost:8080'

/**
 * Vite configuration for the CareLoop dashboard.
 *
 * @remarks
 * `/api` is where the app calls the CareLoop API. With `CARELOOP_MOCK_API=1` the dev server and
 * `vite preview` answer it from the local mock API in `mock/`, which is loaded only then and never
 * reaches the built app; `CARELOOP_MOCK_NOW` pins the moment its fixtures describe. Otherwise both
 * forward `/api`, without the prefix, to `CARELOOP_API_PROXY` (`http://localhost:8080` by default,
 * where Docker Compose publishes the API). Settings are read from the environment or `.env` files.
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
      target: env.CARELOOP_API_PROXY || DEFAULT_API_PROXY,
      changeOrigin: true,
      rewrite: (path) => path.replace(/^\/api/, ''),
    },
  }
  return { plugins, server: { proxy }, preview: { proxy } }
})
