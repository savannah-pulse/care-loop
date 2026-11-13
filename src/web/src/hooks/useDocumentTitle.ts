import { useEffect } from 'react'
import { t } from '../i18n'

/**
 * Sets the browser tab's title for the current screen.
 *
 * @param page - The screen's name, for example `Worklist`.
 *
 * @remarks
 * Side effect: writes `document.title` as `{page} · CareLoop`, which screen readers announce when
 * the screen changes.
 */
export function useDocumentTitle(page: string): void {
  useEffect(() => {
    document.title = t('app.documentTitle', { page })
  }, [page])
}
