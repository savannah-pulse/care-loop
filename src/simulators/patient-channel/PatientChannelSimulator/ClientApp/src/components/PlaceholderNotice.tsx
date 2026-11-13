import { useTranslation } from 'react-i18next';

/** Props for {@link PlaceholderNotice}. */
export interface PlaceholderNoticeProps {
  /** i18n keys of the behaviours planned for the screen, shown as a list. */
  plannedKeys: readonly string[];
}

/**
 * Notice shown on a scaffolded screen that has no behaviour yet.
 *
 * @remarks
 * Used by every placeholder page until workstream WS8 builds the screen; it lists what the
 * screen will do so the scaffold documents its own scope.
 */
export function PlaceholderNotice({ plannedKeys }: PlaceholderNoticeProps) {
  const { t } = useTranslation();

  return (
    <aside className="placeholder-notice">
      <p>{t('placeholder.notice')}</p>
      <strong>{t('placeholder.plannedHeading')}</strong>
      <ul>
        {plannedKeys.map((key) => (
          <li key={key}>{t(key)}</li>
        ))}
      </ul>
    </aside>
  );
}
