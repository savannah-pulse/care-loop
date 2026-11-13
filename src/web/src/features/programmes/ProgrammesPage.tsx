import { NotAvailableState } from '../../components'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

/**
 * Screen 4, programmes: each screening programme's steps, deadlines, evidence and owners.
 *
 * @remarks
 * The API cannot share programme settings yet (`GET /programmes`, C1), so the screen says so calmly.
 * It calls nothing until that endpoint is in the contract.
 */
export function ProgrammesPage() {
  useDocumentTitle(t('programmesPage.title'))
  return (
    <div className="page">
      <div className="head">
        <div>
          <h1>{t('programmesPage.title')}</h1>
          <p className="sub">{t('programmesPage.subtitle')}</p>
        </div>
      </div>
      <NotAvailableState title={t('programmesPage.notAvailableTitle')} body={t('programmesPage.notAvailableBody')} />
    </div>
  )
}
