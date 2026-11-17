import { useTranslation } from 'react-i18next';

/**
 * A small label that marks one record as simulated.
 *
 * @remarks
 * Every record the simulator shows is synthetic, and each one says so where it is shown, in
 * addition to the banner above every screen.
 */
export function SimulatedBadge() {
  const { t } = useTranslation();

  return <span className="badge badge-simulated">{t('common.simulated')}</span>;
}
