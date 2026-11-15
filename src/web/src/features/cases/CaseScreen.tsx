import { useCallback, useState } from 'react'
import type { CareLoopApi, CaseDetail, HandoffSummary, StaffIdentity } from '../../api/client'
import { useResource } from '../../api/useResource'
import { ResourceStatus } from '../../components/ResourceStatus'
import { dateText, durationText } from '../../format'
import { codeText, t } from '../../i18n'

/** Case views share a coordination record but never add clinical write actions. */
export type CaseView = 'case' | 'escalations' | 'summary'

/** Props for the case, escalation and handoff-summary screens. */
export interface CaseScreenProps {
  /** Current-identity API operations. */
  api: CareLoopApi
  /** Staff identity used to show role-appropriate controls. */
  identity: StaffIdentity
  /** Identity boundary for request state. */
  identityKey: string
  /** The selected case. */
  caseId: string
  /** Recorded access reason for reads outside the reader's facility. */
  reason: string
  /** Selected subview. */
  view: CaseView
  /** Selects another subview without changing the case. */
  onView: (view: CaseView) => void
  /** Opens another screening case, retaining the access reason. */
  onOpen: (id: string, reason: string) => void
  /** Returns to the worklist. */
  onBack: () => void
}

/** Shows the case's owner, tracks, received-order timeline, escalation history and human-reviewed draft workflow. */
export function CaseScreen({ api, identity, identityKey, caseId, reason, view, onView, onOpen, onBack }: CaseScreenProps) {
  const [refresh, setRefresh] = useState(0)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [confirmedEscalation, setConfirmedEscalation] = useState('')
  const load = useCallback(async (signal: AbortSignal) => {
    const [detail, timeline] = await Promise.all([api.detail(caseId, reason, signal), api.timeline(caseId, reason, signal)])
    const programme = await api.programme(detail.pathwayId, signal)
    return { detail, timeline, programme }
  }, [api, caseId, reason])
  const resource = useResource(`${identityKey}/${caseId}/${reason}/${refresh}`, load)
  const detail = resource.data?.detail
  const current = detail?.escalations.filter((e) => e.resolvedAt == null).at(-1)
  const doctor = identity.role === 'clinician' || identity.role === 'receiving-clinician'
  async function acknowledge(id: string) {
    setBusy(true); setActionError(null)
    try {
      await api.acknowledge(caseId, id, reason)
      setConfirmedEscalation(''); setRefresh((n) => n + 1)
    } catch (error) {
      setActionError(error instanceof Error ? error.message : t('error.network'))
    } finally { setBusy(false) }
  }
  return <section>
    <button className="link" onClick={onBack}>{t('common.back')}</button>
    <div className="page-heading"><div><h1>{t(view === 'case' ? 'case.title' : view === 'escalations' ? 'escalation.title' : 'summary.title')}</h1>
      {detail && <p>{detail.patientDisplayName ?? detail.caseId}<small>{detail.patientSecondaryIdentifier
        ?? (identity.role === 'coordinator' ? null : detail.patientReference)}</small></p>}</div>
      <button onClick={() => setRefresh((n) => n + 1)} disabled={busy}>{t('common.refresh')}</button></div>
    <nav className="tabs" aria-label={t('nav.case')}>{(['case', 'escalations', 'summary'] as const).map((tab) =>
      <button key={tab} className={view === tab ? 'active' : ''} onClick={() => onView(tab)}>{t(`nav.${tab}`)}</button>)}</nav>
    <ResourceStatus loading={resource.loading} error={resource.error ?? actionError} />
    {detail && resource.data && <>
      <div className="card facts">
        <div><small>{t('common.stage')}</small><strong>{codeText('label', detail.displayLabel)}</strong></div>
        <div><small>{t('common.owner')}</small><strong>{detail.owner.reference}</strong><small>{codeText('role', detail.owner.role)}</small></div>
        <div><small>{t('common.action')}</small><strong>{codeText('action', detail.nextAction)}</strong></div>
        <div><small>{t('common.due')}</small><strong>{dateText(detail.dueAt)}</strong></div>
      </div>
      {detail.proposedOwner && <p className="notice">{t('case.proposedOwner')}: {detail.proposedOwner.reference}. {t('case.handoffNotice')}</p>}
      {view === 'case' && <>
        <div className="grid two">
          <div className="card"><h2>{t('case.tracks')}</h2>{detail.tracks.map((track) =>
            <div className="detail-row" key={track.kind}><div><strong>{codeText('track', track.kind)}</strong><small>{dateText(track.updatedAt)}</small></div>
              <span className={`badge ${track.isComplete ? 'success' : 'warning'}`}>{codeText('status', track.status)}<small>{t(track.isComplete ? 'case.complete' : 'case.incomplete')}</small></span></div>)}
            <h3>{t('case.requiredTracks')}</h3><ul>{resource.data.programme.tracks.filter((track) => track.requiredForClosure)
              .map((track) => <li key={track.kind}>{codeText('track', track.kind)}</li>)}</ul></div>
          <div className="card"><h2>{t('common.evidence')}</h2><p>{detail.outcomeEvidenceReference ?? t('common.none')}</p>
            <p>{detail.outcomeEvidenceKind ?? t('common.none')}</p><dl><dt>{t('case.created')}</dt><dd>{dateText(detail.createdAt)}</dd>
              <dt>{t('case.updated')}</dt><dd>{dateText(detail.updatedAt)}</dd>
              <dt>{t('common.source')}</dt><dd>{detail.sourceSystem}</dd></dl><p className="muted">{t('common.readOnly')}</p></div>
        </div>
      </>}
      {view === 'escalations' && <div className="card">
        <h2>{t('escalation.title')}</h2><p className="notice">{t('escalation.notice')}</p>
        <div className="ladder">{resource.data.programme.escalationLadder.map((step) =>
          <div key={step.level}><strong>{t('escalation.level', { level: step.level })}</strong>
            <p>{codeText('role', step.escalateToRole)}</p><small>{durationText(step.afterOverdue)}</small></div>)}</div>
        {detail.escalations.length === 0 && <p>{t('escalation.noEscalations')}</p>}
        {detail.escalations.map((escalation) => <article className="escalation" key={escalation.escalationId}>
          <h3>{t('escalation.level', { level: escalation.level })} · {codeText('role', escalation.escalatedTo.role)}</h3>
          <p>{escalation.reason}</p><small>{t('escalation.raised')}: {dateText(escalation.raisedAt)}</small>
          {escalation.acknowledgedAt && <p>{t('escalation.acknowledged')}: {escalation.acknowledgedBy} · {dateText(escalation.acknowledgedAt)}</p>}
          {escalation.resolvedAt && <p>{t('escalation.resolved')}: {dateText(escalation.resolvedAt)}</p>}
          {detail.stage !== 'Closed' && current?.escalationId === escalation.escalationId && escalation.acknowledgedAt == null
            && identity.role === escalation.escalatedTo.role && <div>
              <label className="checkbox"><input type="checkbox" checked={confirmedEscalation === escalation.escalationId}
                onChange={(event) => setConfirmedEscalation(event.target.checked ? escalation.escalationId : '')} />{t('escalation.confirm')}</label>
              <button className="primary" disabled={busy || confirmedEscalation !== escalation.escalationId}
                onClick={() => void acknowledge(escalation.escalationId)}>{t('escalation.acknowledge')}</button>
            </div>}
        </article>)}
      </div>}
      {view === 'summary' && (doctor ? <SummaryReview api={api} caseId={caseId} /> : <p className="notice">{t('summary.noPermission')}</p>)}
      {doctor && <OtherCases api={api} detail={detail} identityKey={identityKey} reason={reason} onOpen={onOpen} />}
      {view !== 'summary' && <div className="card"><h2>{t('case.timeline')}</h2><ol className="timeline">
        {resource.data.timeline.map((entry) => <li key={entry.auditEventId}>
          <div><time>{dateText(entry.receivedTimestamp)}</time><small>{t('case.sourceTime')}: {dateText(entry.sourceTimestamp)}</small></div>
          <div><strong>{codeText('event', entry.eventType)}</strong><p>{entry.reason}</p>
            <small>{entry.actorReference} · {codeText('role', entry.actorRole)} · {entry.sourceSystem}</small>
            {entry.evidenceReference && <p>{t('common.evidence')}: {entry.evidenceReference}</p>}</div>
          <span className={`badge ${entry.disposition === 'Rejected' ? 'danger' : 'success'}`}>{codeText('status', entry.disposition)}</span>
        </li>)}</ol></div>}
    </>}
  </section>
}

function SummaryReview({ api, caseId }: { api: CareLoopApi; caseId: string }) {
  const [summary, setSummary] = useState<HandoffSummary | null>(null)
  const [text, setText] = useState('')
  const [reason, setReason] = useState('')
  const [confirmed, setConfirmed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function action(kind: 'draft' | 'approve' | 'reject') {
    setBusy(true); setError(null)
    try {
      const result = kind === 'draft' ? await api.draft(caseId)
        : kind === 'approve' && summary ? await api.approve(caseId, summary.summaryId, text)
          : summary ? await api.reject(caseId, summary.summaryId, reason) : null
      if (result) { setSummary(result); setText(result.text); setConfirmed(false) }
    } catch (failure) { setError(failure instanceof Error ? failure.message : t('error.network')) }
    finally { setBusy(false) }
  }
  return <div className="card"><h2>{t('summary.title')}</h2><p className="notice">{t('summary.notice')}</p>
    <ResourceStatus loading={busy} error={error} />
    <button disabled={busy || summary?.status === 'Approved'} onClick={() => void action('draft')}>{t('summary.draft')}</button>
    {summary == null ? <p>{t('summary.noDraft')}</p> : <>
      <p><span className="badge">{codeText('status', summary.status)}</span></p>
      {summary.sections.map((section, index) => <article key={index}><h3>{section.heading}</h3><p>{section.text}</p>
        <small>{t('summary.sources')}: {section.sourceFactIds.join(', ')}</small></article>)}
      <h3>{t('summary.gaps')}</h3><ul>{summary.missingOrConflictingFacts.map((gap, index) => <li key={index}>{gap}</li>)}</ul>
      <label>{t('summary.text')}<textarea value={text} disabled={busy || summary.status !== 'Draft'}
        onChange={(event) => { setText(event.target.value); setConfirmed(false) }} rows={10} /></label>
      <label className="checkbox"><input type="checkbox" checked={confirmed} disabled={summary.status !== 'Draft'}
        onChange={(event) => setConfirmed(event.target.checked)} />{t('summary.confirm')}</label>
      <button className="primary" disabled={busy || summary.status !== 'Draft' || !confirmed || !text.trim()}
        onClick={() => void action('approve')}>{t('summary.approve')}</button>
      <label>{t('summary.rejection')}<input value={reason} disabled={busy || summary.status !== 'Draft'}
        onChange={(event) => setReason(event.target.value)} /></label>
      <button disabled={busy || summary.status !== 'Draft' || !reason.trim()} onClick={() => void action('reject')}>{t('summary.reject')}</button>
      <h3>{t('summary.provenance')}</h3><p>{summary.draftedBy} · {summary.modelDeployment} · {summary.promptVersion} · {dateText(summary.draftedAt)}</p>
    </>}
  </div>
}

function OtherCases({ api, detail, identityKey, reason, onOpen }: {
  api: CareLoopApi; detail: CaseDetail; identityKey: string; reason: string; onOpen: (id: string, reason: string) => void
}) {
  const [skip, setSkip] = useState(0)
  const load = useCallback((signal: AbortSignal) => api.cases({
    patientReference: detail.patientReference, facilityId: detail.facilityId, reason, skip, take: 50,
  }, signal), [api, detail.patientReference, detail.facilityId, reason, skip])
  const resource = useResource(`${identityKey}/other/${detail.caseId}/${detail.patientReference}/${detail.facilityId}/${reason}/${skip}`, load)
  const others = resource.data?.filter((row) => row.caseId !== detail.caseId)
  return <aside className="card"><h2>{t('case.otherCases')}</h2><p className="muted">{t('case.otherNotice')}</p>
    <ResourceStatus loading={resource.loading} error={resource.error} />
    {others && <ul>{others.map((row) => <li key={row.caseId}><button className="link" onClick={() => onOpen(row.caseId, reason)}>
      {row.pathwayId} · {codeText('label', row.displayLabel)}</button><small>{dateText(row.createdAt)} · {codeText('closure', row.closureReason)}</small></li>)}</ul>}
    {others?.length === 0 && <p>{t('case.noOther')}</p>}
    <div className="pagination"><button disabled={skip === 0 || resource.loading} onClick={() => setSkip((n) => n - 50)}>{t('common.previous')}</button>
      <button disabled={resource.data == null || resource.data.length < 50 || resource.loading} onClick={() => setSkip((n) => n + 50)}>{t('common.next')}</button></div>
  </aside>
}
