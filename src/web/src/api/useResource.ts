import { useEffect, useState } from 'react'
import { t } from '../i18n'

/** An identity-keyed request state that never exposes a previous reader's cached result. */
export interface Resource<T> {
  /** Successful data belonging to the current request key only. */
  data: T | null
  /** Whether the current request is pending. */
  loading: boolean
  /** An explicit localized request failure, if any. */
  error: string | null
}

/**
 * Loads a read resource and cancels it when the identity, route or refresh key changes.
 * @param key - Identity plus route/filter/refresh key.
 * @param load - A stable callback performing cancellable reads.
 * @returns Current-key data, loading state and explicit errors.
 * @remarks Performs network calls; it does not poll, cache patient data or silently substitute an empty result.
 */
export function useResource<T>(key: string, load: (signal: AbortSignal) => Promise<T>): Resource<T> {
  const [state, setState] = useState<Resource<T> & { key: string }>({ key: '', data: null, loading: true, error: null })
  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal).then(
      (data) => { if (!controller.signal.aborted) setState({ key, data, loading: false, error: null }) },
      (error: unknown) => {
        if (!controller.signal.aborted) setState({
          key, data: null, loading: false, error: error instanceof Error ? error.message : t('error.network'),
        })
      },
    )
    return () => controller.abort()
  }, [key, load])
  return state.key === key ? state : { data: null, loading: true, error: null }
}
