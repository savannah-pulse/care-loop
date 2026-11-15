import { useCallback, useState } from 'react'
import type { CareLoopApi } from '../../api/client'
import { useResource } from '../../api/useResource'
import { ResourceStatus } from '../../components/ResourceStatus'
import { CaseTable } from '../cases/CaseTable'
import { codeText, t } from '../../i18n'

/** Props for the worklist and exception queues. */
export interface WorklistProps {
  /** The current-identity API client. */
  api: CareLoopApi
  /** Identity cache boundary; changing it immediately hides all prior data. */
  identityKey: string
  /** Opens a case with the current access reason. */
  onOpen: (id: string, reason: string) => void
}

/** The worklist and exception queues, with paging, current-page search and documented facility access. */
export function Worklist({ api, identityKey, onOpen }: WorklistProps) {
  const [skip, setSkip] = useState(0)
  const [queue, setQueue] = useState('')
  const [search, setSearch] = useState('')
  const [facilityDraft, setFacilityDraft] = useState('')
  const [reasonDraft, setReasonDraft] = useState('')
  const [filters, setFilters] = useState({ facility: '', reason: '' })
  const [refresh, setRefresh] = useState(0)
  const load = useCallback(async (signal: AbortSignal) => {
    const query = { facilityId: filters.facility, reason: filters.reason }
    const [cases, exceptions] = await Promise.all([
      api.cases({ ...query, skip, take: 50 }, signal), api.exceptions(query, signal),
    ])
    return { cases, exceptions }
  }, [api, filters, skip])
  const resource = useResource(`${identityKey}/${skip}/${JSON.stringify(filters)}/${refresh}`, load)
  const rows = resource.data == null ? [] : queue
    ? resource.data.exceptions.filter((row) => row.exceptions.includes(queue)) : resource.data.cases
  const filtered = rows.filter((row) =>
    [row.patientDisplayName, row.patientSecondaryIdentifier, row.patientReference, row.caseId]
      .some((value) => value?.toLowerCase().includes(search.toLowerCase())))
  const queues = ['Overdue', 'Rejected', 'Unreachable', 'Unacknowledged', 'Unassigned']
  return <section>
    <div className="page-heading"><div><h1>{t('worklist.title')}</h1><p>{t('worklist.description')}</p></div>
      <button onClick={() => setRefresh((n) => n + 1)}>{t('common.refresh')}</button></div>
    <form className="filters" onSubmit={(event) => {
      event.preventDefault(); setSkip(0); setFilters({ facility: facilityDraft.trim(), reason: reasonDraft.trim() })
    }}>
      <label>{t('worklist.facility')}<input value={facilityDraft} onChange={(event) => setFacilityDraft(event.target.value)} maxLength={100} /></label>
      <label>{t('worklist.accessReason')}<input value={reasonDraft} onChange={(event) => setReasonDraft(event.target.value)} maxLength={1000} /></label>
      <button type="submit">{t('worklist.apply')}</button>
    </form>
    <div className="chips"><button className={!queue ? 'active' : ''} onClick={() => setQueue('')}>{t('worklist.allCases')}</button>
      {queues.map((flag) => <button key={flag} className={queue === flag ? 'active' : ''} onClick={() => setQueue(flag)}>
        {codeText('exception', flag)}{resource.data == null ? '' : ` (${resource.data.exceptions.filter((r) => r.exceptions.includes(flag)).length})`}
      </button>)}</div>
    <label className="search">{t('worklist.search')}<input type="search" value={search} onChange={(event) => setSearch(event.target.value)} /></label>
    <ResourceStatus loading={resource.loading} error={resource.error} />
    {resource.data && <CaseTable rows={filtered} onOpen={(id) => onOpen(id, filters.reason)} />}
    {!queue && <div className="pagination"><button disabled={skip === 0 || resource.loading}
      onClick={() => setSkip((n) => Math.max(0, n - 50))}>{t('common.previous')}</button>
      <button disabled={resource.loading || resource.data == null || resource.data.cases.length < 50}
        onClick={() => setSkip((n) => n + 50)}>{t('common.next')}</button></div>}
    <p className="muted">{t('worklist.pageNote')}</p><p className="muted">{t('common.readOnly')}</p>
  </section>
}
