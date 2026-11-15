import { useTranslation } from 'react-i18next';
import { SimulatorApiError } from '../api/simulator';

/** Props for explicit network and pending feedback. */
interface ApiStatusProps {
  /** Error from a simulator request, not a silently empty result. */
  error: unknown;
  /** Whether the initial request is pending. */
  pending?: boolean;
}
/** Displays localized HTTP/network failures and initial loading status. */
export function ApiStatus({ error, pending }: ApiStatusProps) {
  const { t } = useTranslation();
  if (error !== null && error !== undefined)
    return (
      <p role="alert">
        {error instanceof SimulatorApiError
          ? t('common.httpError', { status: error.status })
          : t('common.networkError')}
      </p>
    );
  return pending ? <p role="status">{t('common.loading')}</p> : null;
}
