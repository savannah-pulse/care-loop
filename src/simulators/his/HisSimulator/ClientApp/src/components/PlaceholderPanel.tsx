import { useTranslation } from 'react-i18next';

/** Props for {@link PlaceholderPanel}. */
export interface PlaceholderPanelProps {
  /** Key in `locales/en.json` for the screen's heading. */
  titleKey: string;
  /** Key in `locales/en.json` for the sentence describing what the screen is for. */
  descriptionKey: string;
  /** Keys in `locales/en.json`, one per behaviour planned for the screen, shown as a list. */
  plannedKeys: readonly string[];
}

/**
 * The body of a screen that is scaffolded but not built yet.
 *
 * @remarks
 * Shows the screen's purpose and the behaviours planned for it, so the scaffold is honest about
 * what exists. Each feature page renders this until WS7 replaces it with the real screen.
 */
export function PlaceholderPanel({ titleKey, descriptionKey, plannedKeys }: PlaceholderPanelProps) {
  const { t } = useTranslation();

  return (
    <section className="placeholder-panel">
      <h2>{t(titleKey)}</h2>
      <p>{t(descriptionKey)}</p>
      <p className="placeholder-panel__notice">{t('placeholder.notice')}</p>
      <h3>{t('placeholder.planned')}</h3>
      <ul>
        {plannedKeys.map((key) => (
          <li key={key}>{t(key)}</li>
        ))}
      </ul>
    </section>
  );
}
