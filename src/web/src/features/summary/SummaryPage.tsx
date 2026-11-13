import { useEffect, useId, useRef, useState } from 'react'
import { Badge, Icon, NotAvailableState, ReadView } from '../../components'
import {
  approveHandoffSummary,
  draftHandoffSummary,
  rejectHandoffSummary,
  useCase,
  type ApiFailure,
  type CaseDetail,
  type FailureKind,
  type HandoffSummary,
  type SummarySection,
} from '../../data'
import { programmeName } from '../../domain'
import { formatDateTime, parseInstant } from '../../format'
import { useDocumentTitle } from '../../hooks'
import { findStaffByReference, isClinicianRole, useSignedInStaff, type StaffMember } from '../../identity'
import { t, type MessageKey } from '../../i18n'
import { routeHref } from '../../routing'
import { applyEdits, approvedTextFor, hasEdits, isAssessmentSection, type SectionEdits } from './summary'

/** Props for {@link SummaryPage}. */
export interface SummaryPageProps {
  /** CareLoop's identifier of the case the summary is for, from the address. */
  readonly caseId: string
}

type DraftState =
  | { readonly step: 'idle' }
  | { readonly step: 'drafting' }
  | { readonly step: 'failed'; readonly failure: ApiFailure }
  | { readonly step: 'ready'; readonly summary: HandoffSummary }

const NO_DRAFT_MESSAGES: Readonly<Partial<Record<FailureKind, MessageKey>>> = {
  notAvailable: 'summary.noDraft.notAvailable',
  conflict: 'summary.noDraft.conflict',
  unavailable: 'summary.noDraft.unavailable',
}

const DECISION_MESSAGES: Readonly<Partial<Record<FailureKind, MessageKey>>> = {
  forbidden: 'summary.decisionFailed.forbidden',
  conflict: 'summary.decisionFailed.conflict',
  notAvailable: 'summary.decisionFailed.notAvailable',
  notFound: 'summary.decisionFailed.notFound',
  invalid: 'summary.decisionFailed.invalid',
}

function personName(reference: string | null | undefined, me: StaffMember): string | undefined {
  if (!reference || reference === me.reference) {
    return undefined
  }
  return findStaffByReference(reference)?.name ?? reference
}

function when(value: string | null | undefined): string {
  const date = parseInstant(value)
  return date ? formatDateTime(date) : ''
}

interface SectionBlockProps {
  readonly section: SummarySection
  readonly index: number
  readonly editing: boolean
  readonly onEdit: (index: number, text: string) => void
}

function SectionBlock({ section, index, editing, onEdit }: SectionBlockProps) {
  const fieldId = useId()
  const assessment = isAssessmentSection(section)
  const sources =
    section.sourceFactIds.length > 0 ? (
      <ul className="srcs" aria-label={t('summary.sources')}>
        {section.sourceFactIds.map((id) => (
          <li key={id}>
            <Icon name="doc" />
            {id}
          </li>
        ))}
      </ul>
    ) : null
  if (editing && !assessment) {
    return (
      <div>
        <label htmlFor={fieldId}>{section.heading}</label>
        <textarea
          id={fieldId}
          className="textarea"
          value={section.text}
          rows={Math.max(3, Math.ceil(section.text.length / 90))}
          onChange={(event) => onEdit(index, event.target.value)}
        />
        {sources}
      </div>
    )
  }
  return (
    <div>
      <h3>{section.heading}</h3>
      <p className="section-text">{section.text}</p>
      {assessment ? <p className="note">{t('summary.assessmentNote')}</p> : sources}
    </div>
  )
}

interface DecisionProps {
  readonly summary: HandoffSummary
  readonly me: StaffMember
}

function Decision({ summary, me }: DecisionProps) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    ref.current?.focus()
  }, [summary.status])
  if (summary.status === 'Approved') {
    const name = personName(summary.approvedBy, me)
    const date = when(summary.approvedAt)
    return (
      <div className="approved" role="status" tabIndex={-1} ref={ref}>
        <Icon name="check" />
        <div className="decision-text">
          {name ? t('summary.approvedBy', { name, date }) : t('summary.approvedByYou', { date })}
          <span>{t('summary.approvedNext')}</span>
        </div>
      </div>
    )
  }
  if (summary.status === 'Rejected') {
    const name = personName(summary.rejectedBy, me)
    const date = when(summary.rejectedAt)
    return (
      <div className="rejected" role="status" tabIndex={-1} ref={ref}>
        <Icon name="x" />
        <div className="decision-text">
          {name ? t('summary.rejectedBy', { name, date }) : t('summary.rejectedByYou', { date })}
          {summary.rejectionReason ? (
            <span>{t('summary.rejectedReason', { reason: summary.rejectionReason })}</span>
          ) : null}
          <span>{t('summary.rejectedNext')}</span>
        </div>
      </div>
    )
  }
  return null
}

interface DraftViewProps {
  readonly caseId: string
  readonly initial: HandoffSummary
  readonly me: StaffMember
}

function DraftView({ caseId, initial, me }: DraftViewProps) {
  const [summary, setSummary] = useState(initial)
  const [edits, setEdits] = useState<SectionEdits>({})
  const [editing, setEditing] = useState(false)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [reasonMissing, setReasonMissing] = useState(false)
  const [busy, setBusy] = useState<'approve' | 'reject'>()
  const [decisionError, setDecisionError] = useState<FailureKind>()
  const ids = useId()
  const headingRef = useRef<HTMLHeadingElement>(null)
  const reasonRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  useEffect(() => {
    if (rejecting) {
      reasonRef.current?.focus()
    }
  }, [rejecting])

  const sections = applyEdits(summary.sections, edits)
  const edited = hasEdits(summary.sections, edits)
  const open = summary.status === 'Draft'
  const facts = summary.missingOrConflictingFacts

  const onEdit = (index: number, text: string): void => setEdits((current) => ({ ...current, [index]: text }))

  const approve = async (): Promise<void> => {
    setBusy('approve')
    setDecisionError(undefined)
    const outcome = await approveHandoffSummary(caseId, summary.summaryId, approvedTextFor(summary, edits))
    setBusy(undefined)
    if (outcome.ok) {
      setSummary(outcome.data)
      setEditing(false)
      setRejecting(false)
    } else {
      setDecisionError(outcome.failure.kind)
    }
  }

  const reject = async (): Promise<void> => {
    if (reason.trim() === '') {
      setReasonMissing(true)
      reasonRef.current?.focus()
      return
    }
    setBusy('reject')
    setDecisionError(undefined)
    const outcome = await rejectHandoffSummary(caseId, summary.summaryId, reason.trim())
    setBusy(undefined)
    if (outcome.ok) {
      setSummary(outcome.data)
      setEditing(false)
      setRejecting(false)
    } else {
      setDecisionError(outcome.failure.kind)
    }
  }

  return (
    <>
      <div className="grid2">
        <section className="card" aria-labelledby={`${ids}-heading`}>
          <h2 className="card-title" id={`${ids}-heading`} tabIndex={-1} ref={headingRef}>
            {t('summary.cardTitle')}
            <span className="r btns">
              {edited ? <Badge tone="blue">{t('summary.editedByYou')}</Badge> : null}
              <span className="ai">
                <Icon name="spark" />
                {t('summary.aiDraft')}
              </span>
            </span>
          </h2>
          <Decision summary={summary} me={me} />
          <div className="sbar linkrow">
            {sections.map((section, index) => (
              <SectionBlock
                key={`${index}-${section.heading}`}
                section={section}
                index={index}
                editing={editing && open}
                onEdit={onEdit}
              />
            ))}
          </div>
          <p className="note linkrow">{t('summary.aiNote')}</p>
        </section>
        <div className="stack">
          <section className="card" aria-labelledby={`${ids}-check`}>
            <h2 className="card-title" id={`${ids}-check`}>
              {t('summary.checkTitle')}
            </h2>
            {facts.length > 0 ? (
              <ul className="check">
                {facts.map((fact) => (
                  <li key={fact}>
                    <span className="mk todo" aria-hidden="true" />
                    <span>{fact}</span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="note">{t('summary.checkNone')}</p>
            )}
          </section>
          <section className="card" aria-labelledby={`${ids}-kept`}>
            <h2 className="card-title" id={`${ids}-kept`}>
              {t('summary.keptTitle')}
            </h2>
            <ul className="reach">
              <li>
                <Icon name="user" />
                <span>{t('summary.keptWho')}</span>
              </li>
              <li>
                <Icon name="doc" />
                <span>{t('summary.keptText')}</span>
              </li>
              <li>
                <Icon name="doc" />
                <span>{t('summary.keptFacts')}</span>
              </li>
              <li>
                <Icon name="spark" />
                <span>
                  {t('summary.keptModel', { model: summary.modelDeployment, prompt: summary.promptVersion })}
                </span>
              </li>
            </ul>
            <p className="note linkrow">{t('summary.keptNote')}</p>
          </section>
        </div>
      </div>

      {open && rejecting ? (
        <section className="card" id={`${ids}-reject`}>
          <label className="card-title" htmlFor={`${ids}-reason`}>
            {t('summary.rejectTitle')}
          </label>
          <p className="note" id={`${ids}-reason-help`}>
            {t('summary.rejectHelp')}
          </p>
          <textarea
            ref={reasonRef}
            id={`${ids}-reason`}
            className="textarea linkrow"
            value={reason}
            aria-invalid={reasonMissing}
            aria-describedby={reasonMissing ? `${ids}-reason-help ${ids}-reason-error` : `${ids}-reason-help`}
            onChange={(event) => {
              setReason(event.target.value)
              setReasonMissing(false)
            }}
          />
          {reasonMissing ? (
            <p className="field-error" id={`${ids}-reason-error`}>
              {t('summary.rejectReasonRequired')}
            </p>
          ) : null}
          <div className="btns linkrow">
            <button type="button" className="btn" disabled={busy !== undefined} onClick={() => void reject()}>
              {busy === 'reject' ? t('summary.rejecting') : t('summary.rejectConfirm')}
            </button>
            <button type="button" className="btn" disabled={busy !== undefined} onClick={() => setRejecting(false)}>
              {t('summary.cancel')}
            </button>
          </div>
        </section>
      ) : null}

      {open ? (
        <div className="actionbar" role="group" aria-label={t('summary.actionsLabel')}>
          {decisionError ? (
            <p className="field-error grow" role="alert">
              {t(DECISION_MESSAGES[decisionError] ?? 'summary.decisionFailed.error')}
            </p>
          ) : null}
          <div className="btns">
            <button
              type="button"
              className="btn"
              aria-expanded={rejecting}
              aria-controls={`${ids}-reject`}
              disabled={busy !== undefined}
              onClick={() => setRejecting((value) => !value)}
            >
              {t('summary.reject')}
            </button>
            <button
              type="button"
              className="btn"
              disabled={busy !== undefined}
              onClick={() => setEditing((value) => !value)}
            >
              {editing ? t('summary.stopEditing') : t('summary.edit')}
            </button>
            <button
              type="button"
              className="btn primary"
              disabled={busy !== undefined}
              onClick={() => void approve()}
            >
              {busy === 'approve' ? t('summary.approving') : t('summary.approve')}
            </button>
          </div>
        </div>
      ) : null}
    </>
  )
}

interface WorkspaceProps {
  readonly detail: CaseDetail
}

function SummaryWorkspace({ detail }: WorkspaceProps) {
  const me = useSignedInStaff()
  const [draft, setDraft] = useState<DraftState>({ step: 'idle' })
  const ids = useId()

  const requestDraft = async (): Promise<void> => {
    setDraft({ step: 'drafting' })
    const outcome = await draftHandoffSummary(detail.caseId)
    setDraft(outcome.ok ? { step: 'ready', summary: outcome.data } : { step: 'failed', failure: outcome.failure })
  }

  const drafted = draft.step === 'ready' ? parseInstant(draft.summary.draftedAt) : undefined

  return (
    <>
      <div className="head">
        <div>
          <h1>{t('summary.title')}</h1>
          <p className="sub">
            {drafted ? t('summary.draftedAt', { date: formatDateTime(drafted) }) : t('summary.subtitle')}
          </p>
        </div>
      </div>
      <div className="pbanner compact">
        <span className="av" aria-hidden="true">
          <Icon name="user" />
        </span>
        <div className="who">
          <p className="nm">{detail.patientReference}</p>
          <p className="id">
            <span>{programmeName(detail.pathwayId)}</span>
            <span>{t('case.resultFrom', { result: detail.sourceResultId, system: detail.sourceSystem })}</span>
          </p>
        </div>
      </div>

      {!isClinicianRole(me.role) ? (
        <NotAvailableState
          icon="lock"
          title={t('summary.clinicianOnlyTitle')}
          body={t('summary.clinicianOnly', { name: me.name, title: t(me.titleKey) })}
        />
      ) : draft.step === 'ready' ? (
        <DraftView key={draft.summary.summaryId} caseId={detail.caseId} initial={draft.summary} me={me} />
      ) : draft.step === 'failed' ? (
        <section className="card" aria-labelledby={`${ids}-none`}>
          <h2 className="card-title" id={`${ids}-none`}>
            {t('summary.noDraftTitle')}
          </h2>
          <div className="notice" role="status">
            <Icon name="spark" />
            <span>{t(NO_DRAFT_MESSAGES[draft.failure.kind] ?? 'summary.noDraft.error')}</span>
          </div>
          <p className="note linkrow">{t('summary.noDraftBody')}</p>
          {draft.failure.kind === 'conflict' ? null : (
            <button type="button" className="btn small-btn linkrow" onClick={() => void requestDraft()}>
              {draft.failure.kind === 'notAvailable' ? t('states.checkAgain') : t('states.tryAgain')}
            </button>
          )}
        </section>
      ) : (
        <section className="card" aria-labelledby={`${ids}-start`}>
          <h2 className="card-title" id={`${ids}-start`}>
            {t('summary.startTitle')}
          </h2>
          <p className="note">{t('summary.startBody')}</p>
          <button
            type="button"
            className="btn primary linkrow"
            disabled={draft.step === 'drafting'}
            onClick={() => void requestDraft()}
          >
            {draft.step === 'drafting' ? t('summary.drafting') : t('summary.draft')}
          </button>
          {draft.step === 'drafting' ? (
            <p className="visually-hidden" role="status">
              {t('summary.drafting')}
            </p>
          ) : null}
        </section>
      )}
    </>
  )
}

/**
 * Screen 18, the handoff summary: a clinician asks AI for a draft, checks every line against its
 * sources, edits it if needed and approves or rejects it.
 *
 * @remarks
 * AI output is a draft. `POST /cases/{caseId}/handoff-summary` drafts it; nothing is used until a
 * clinician approves it, and the approval is recorded with their name, the time and the exact text
 * they approved. AI never writes the assessment, so that section cannot be edited here. When there
 * is no draft (AI off, not built yet, or failed), the screen says so plainly and never shows
 * half-made text. Only clinician roles get the actions; the API decides who may approve.
 */
export function SummaryPage({ caseId }: SummaryPageProps) {
  useDocumentTitle(t('summary.title'))
  const read = useCase(caseId)
  return (
    <div className="page">
      <a className="crumb" href={routeHref({ name: 'case', caseId })}>
        <Icon name="left" />
        {read.state.status === 'ready' ? read.state.data.patientReference : t('case.titleFallback')}
      </a>
      <ReadView
        read={read}
        loading={t('case.loading')}
        notAvailable={t('case.notAvailable')}
        errorTitle={t('case.errorTitle')}
        notFound={{ title: t('case.notFoundTitle'), body: t('case.notFoundBody') }}
      >
        {(detail) => <SummaryWorkspace detail={detail} />}
      </ReadView>
    </div>
  )
}
