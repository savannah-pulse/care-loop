/// <reference types="vite/client" />

/**
 * Build-time settings the app reads from `import.meta.env`.
 *
 * @remarks
 * Vite exposes only variables whose names start with `VITE_`, and bakes them into the bundle when
 * it is built.
 */
interface ImportMetaEnv {
  /**
   * Base URL of the CareLoop API, for example `https://careloop.example/api`. When unset, the app
   * calls `/api` on its own origin, which the dev server and the container's nginx forward to the
   * API.
   */
  readonly VITE_CARELOOP_API_URL?: string
}

/** Adds the CareLoop settings to `import.meta.env`. */
interface ImportMeta {
  /** Build-time settings; see {@link ImportMetaEnv}. */
  readonly env: ImportMetaEnv
}
