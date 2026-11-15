import { useCallback, useState } from 'react'
import type { CareLoopApi } from '../../api/client'
import { useResource } from '../../api/useResource'
import { ResourceStatus } from '../../components/ResourceStatus'
import { codeText, t } from '../../i18n'
import { durationText } from '../../format'

/** Props for the read-only programme configuration screen. */
export interface ProgrammesProps {
  /** Current-identity API operations. */
  api: CareLoopApi
  /** Identity boundary for programme reads. */
  identityKey: string
}

/** Lists programmes and shows current deadlines, clinical approval, evidence rules and change history. */
export function Programmes({ api, identityKey }: ProgrammesProps) {
  const [selected, setSelected] = useState('')
  const [refresh, setRefresh] = useState(0)
  const load = useCallback((signal: AbortSignal) => api.programmes(signal), [api])
  const resource = useResource(`${identityKey}/programmes/${refresh}`, load)
  const id = selected || resource.data?.[0]?.pathwayId
  return <section><div className="page-heading"><div><h1>{t('programmes.title')}</h1><p>{t('programmes.description')}</p></div>
    <button onClick={() => setRefresh((n) => n + 1)}>{t('common.refresh')}</button></div>
    <ResourceStatus loading={resource.loading} error={resource.error} />
    {resource.data && <div className="chips">{resource.data.map((programme) =>
      <button className={programme.pathwayId === id ? 'active' : ''} key={programme.pathwayId}
        onClick={() => setSelected(programme.pathwayId)}>{programme.displayName}</button>)}</div>}
    {id && <ProgrammeDetails key={id} api={api} identityKey={`${identityKey}/${refresh}`} id={id} />}
  </section>
}

function ProgrammeDetails({ api, identityKey, id }: { api: CareLoopApi; identityKey: string; id: string }) {
  const load = useCallback((signal: AbortSignal) => api.programme(id, signal), [api, id])
  const resource = useResource(`${identityKey}/programme/${id}`, load)
  const programme = resource.data
  return <><ResourceStatus loading={resource.loading} error={resource.error} />{programme && <>
    <div className="card"><h2>{programme.displayName}</h2><p>{t('programmes.version')}: {programme.version}</p>
      {programme.isPlaceholder && <p className="badge warning">{t('programmes.placeholder')}</p>}
      <h3>{t('programmes.deadlines')}</h3><p className="muted">{t('common.clockTime')}</p><div className="table-scroll"><table>
        <thead><tr><th>{t('common.action')}</th><th>{t('common.urgency')}</th><th>{t('common.owner')}</th>
          <th>{t('programmes.duration')}</th><th>{t('programmes.warning')}</th><th>{t('programmes.citation')}</th><th>{t('programmes.approval')}</th></tr></thead>
        <tbody>{programme.deadlines.map((deadline) => <tr key={`${deadline.nextAction}/${deadline.urgencyCode}`}>
          <td>{codeText('action', deadline.nextAction)}</td><td>{deadline.urgencyCode ?? t('common.all')}</td>
          <td>{codeText('role', deadline.ownerRole)}</td><td>{durationText(deadline.duration)}</td><td>{durationText(deadline.warnBefore)}</td>
          <td>{deadline.citation ?? t('common.none')}</td><td>{t(deadline.clinicallyApproved ? 'programmes.approved' : 'programmes.unapproved')}</td>
        </tr>)}</tbody></table></div>
    </div>
    <div className="grid two"><div className="card"><h2>{t('programmes.closure')}</h2>{programme.closureRules.map((rule) =>
      <details key={rule.reason}><summary>{codeText('closure', rule.reason)}</summary>
        <p>{t('programmes.roles')}: {rule.authorizedRoles.map((role) => codeText('role', role)).join(', ')}</p>
        <ul>{rule.requiresEvidenceReference && <li>{t('programmes.requireEvidence')}</li>}
          {rule.requiresExceptionNote && <li>{t('programmes.requireNote')}</li>}
          {rule.requiresNextSurveillanceDate && <li>{t('programmes.requireDate')}</li>}
          <li>{t('programmes.attempts', { count: rule.minimumDocumentedAttempts })}</li></ul>
        <p>{rule.citation ?? t('common.none')}</p></details>)}</div>
      <div className="card"><h2>{t('programmes.evidenceKinds')}</h2>{programme.evidenceKinds.map((kind) =>
        <div key={kind.kind} className="detail-row"><div><strong>{kind.kind}</strong><p>{kind.description}</p>
          <small>{codeText('action', kind.nextAction)}</small></div><span className="badge">{t(kind.clinicallyApproved ? 'programmes.approved' : 'programmes.unapproved')}</span></div>)}</div></div>
    <div className="card"><h2>{t('programmes.history')}</h2>{programme.changeLog.map((change) =>
      <article key={`${change.version}/${change.date}`}><h3>{change.version} · {change.date}</h3><p>{change.summary}</p><small>{change.approver}</small></article>)}</div>
  </>}</>
}
