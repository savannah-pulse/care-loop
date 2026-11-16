import { EmptyState } from '../../components'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { routeHref } from '../../routing'

/**
 * Shown when the address matches no screen, with a way back to the worklist.
 */
export function NotFoundPage() {
  useDocumentTitle(t('notFound.title'))
  return (
    <div className="page">
      <EmptyState
        level={1}
        title={t('notFound.title')}
        body={t('notFound.body')}
        action={
          <a className="btn" href={routeHref({ name: 'worklist' })}>
            {t('notFound.link')}
          </a>
        }
      />
    </div>
  )
}
