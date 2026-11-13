import { useTranslation } from 'react-i18next';

/**
 * A banner stating that everything on screen is simulated.
 *
 * @remarks
 * CareLoop's prototype handles synthetic data only, and every simulated record must be visibly
 * labelled. The banner is rendered once by {@link App} above every screen and must never be
 * hidden or made dismissible.
 */
export function SimulatedBanner() {
  const { t } = useTranslation();

  return (
    <p className="simulated-banner" role="note" data-testid="simulated-banner">
      {t('banner.simulated')}
    </p>
  );
}
