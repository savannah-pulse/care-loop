import { t } from '../i18n'

/** Props for the common loading and error notice. */
export interface ResourceStatusProps {
  /** Whether the current resource is pending. */
  loading: boolean
  /** Explicit request failure, if present. */
  error: string | null
}

/** Displays an accessible pending state or request refusal, never a success-shaped empty screen. */
export function ResourceStatus({ loading, error }: ResourceStatusProps) {
  return error ? <p className="notice error" role="alert">{error}</p>
    : loading ? <p className="notice" role="status">{t('common.loading')}</p> : null
}
