import { useTranslation } from 'react-i18next';

/**
 * A banner stating that everything on screen is simulated.
 *
 * @remarks
 * CareLoop works with synthetic data only and every simulated record must be visibly labelled.
 * The banner is rendered once by the app shell, above every screen.
 */
export function SimulatedBanner() {
  const { t } = useTranslation();

  return (
    <div className="simulated-banner" role="note" data-testid="simulated-banner">
      {t('banner.simulated')}
    </div>
  );
}
