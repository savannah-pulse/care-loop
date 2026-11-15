import { useMemo, useState } from 'react'
import { createApi } from './api/client'
import type { StaffIdentity } from './api/client'
import { Worklist } from './features/exceptions/Worklist'
import { CaseScreen } from './features/cases/CaseScreen'
import type { CaseView } from './features/cases/CaseScreen'
import { Impact } from './features/metrics/Impact'
import { Programmes } from './features/programmes/Programmes'
import { ErrorBoundary } from './components/ErrorBoundary'
import { codeText, t } from './i18n'
import './App.css'

/** The CareLoop dashboard shell; no patient data is cached in browser storage. */
export default function App() {
  const [identity, setIdentity] = useState<StaffIdentity | null>(null)
  const [screen, setScreen] = useState<'worklist' | 'impact' | 'programmes' | 'case'>('worklist')
  const [selected, setSelected] = useState({ id: '', reason: '' })
  const [caseView, setCaseView] = useState<CaseView>('case')
  const identityKey = JSON.stringify(identity)
  const api = useMemo(() => identity ? createApi(identity) : null, [identity])
  function openCase(id: string, reason: string) {
    setSelected({ id, reason }); setCaseView('case'); setScreen('case')
  }
  return <div className="shell">
    <header className="topbar"><a className="brand" href="#" onClick={(event) => { event.preventDefault(); setScreen('worklist') }}>
      <span aria-hidden="true" className="brand-mark" />{t('app.name')}</a>
      <nav aria-label={t('app.name')}>{(['worklist', 'impact', 'programmes'] as const).map((tab) =>
        <button disabled={!identity} key={tab} className={screen === tab || screen === 'case' && tab === 'worklist' ? 'active' : ''}
          onClick={() => setScreen(tab)}>{t(`nav.${tab}`)}</button>)}</nav>
      <IdentityForm identity={identity} onApply={(next) => {
        setIdentity(next); setSelected({ id: '', reason: '' }); setScreen('worklist')
      }} />
    </header>
    <main><ErrorBoundary key={`${identityKey}/${screen}/${selected.id}`}>
      {!identity || !api ? <section><h1>{t('app.name')}</h1><p>{t('app.tagline')}</p><p className="notice">{t('identity.notice')}</p></section>
        : screen === 'worklist' ? <Worklist key={identityKey} api={api} identityKey={identityKey} onOpen={openCase} />
          : screen === 'impact' ? <Impact api={api} identityKey={identityKey} />
            : screen === 'programmes' ? <Programmes api={api} identityKey={identityKey} />
              : <CaseScreen key={`${identityKey}/${selected.id}`} api={api} identity={identity} identityKey={identityKey}
                caseId={selected.id} reason={selected.reason} view={caseView} onView={setCaseView} onOpen={openCase}
                onBack={() => setScreen('worklist')} />}
    </ErrorBoundary></main>
  </div>
}

function IdentityForm({ identity, onApply }: { identity: StaffIdentity | null; onApply: (identity: StaffIdentity) => void }) {
  const [draft, setDraft] = useState<StaffIdentity>({ user: '', role: 'clinician', facility: '' })
  const [expanded, setExpanded] = useState(identity === null)
  return <details className="identity" open={expanded} onToggle={(event) => setExpanded(event.currentTarget.open)}>
    <summary>{identity ? `${identity.user} · ${codeText('role', identity.role)} · ${identity.facility}` : t('identity.title')}</summary>
    <form onSubmit={(event) => {
      event.preventDefault(); onApply({ ...draft, user: draft.user.trim(), facility: draft.facility.trim() }); setExpanded(false)
    }}>
      <p>{t('identity.notice')}</p>
      <label>{t('identity.user')}<input required maxLength={200} value={draft.user} onChange={(event) => setDraft({ ...draft, user: event.target.value })} /></label>
      <label>{t('identity.role')}<select value={draft.role} onChange={(event) => setDraft({ ...draft, role: event.target.value })}>
        {['clinician', 'nurse-navigator', 'facility-lead', 'coordinator', 'receiving-clinician', 'lab'].map((role) =>
          <option key={role} value={role}>{codeText('role', role)}</option>)}</select></label>
      <label>{t('identity.facility')}<input required={draft.role !== 'coordinator'} maxLength={100} value={draft.facility}
        onChange={(event) => setDraft({ ...draft, facility: event.target.value })} /></label>
      <button className="primary" disabled={!draft.user.trim() || draft.role !== 'coordinator' && !draft.facility.trim()}>{t('identity.apply')}</button>
    </form>
  </details>
}
