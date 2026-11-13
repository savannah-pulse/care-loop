import { EmptyState } from '../../components'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

/**
 * Screen 4, programmes: each screening programme's steps, deadlines and who acts at each step.
 *
 * @remarks
 * The API cannot share programme settings yet (`GET /programmes`, C1), so the screen shows a calm
 * empty state saying what will appear here. It calls nothing until that endpoint is in the contract.
 */
export function ProgrammesPage() {
  useDocumentTitle(t('programmesPage.title'))
  return (
    <div className="page">
      <div className="page-head">
        <h1>{t('programmesPage.title')}</h1>
      </div>
      <EmptyState
        icon="list"
        title={t('programmesPage.notAvailableTitle')}
        body={t('programmesPage.notAvailableBody')}
      />
    </div>
  )
}
