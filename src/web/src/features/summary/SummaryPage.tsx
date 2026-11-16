import { useCallback, useEffect, useId, useRef, useState, type Ref } from 'react'
import { Badge, Dialog, Icon, LoadingState, NotAvailableState, OutsideTeamCard, ReadView, Toast } from '../../components'
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
import { describeOwner, describePatient, programmeName } from '../../domain'
import { factLabel, formatDateTime, parseInstant } from '../../format'
import { useDocumentTitle } from '../../hooks'
import {
  findStaffByReference,
  isClinicianRole,
  rememberAccessReason,
  sameReference,
  useAccessReason,
  useSignedInStaff,
  type StaffMember,
} from '../../identity'
import { t, type MessageKey } from '../../i18n'
import { navigate, routeHref } from '../../routing'
import { OtherScreeningCard } from '../case'
import { forgetOpenDraft, keepOpenDraft, openDraftFor } from './drafts'
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
  | { readonly step: 'ready'; readonly summary: HandoffSummary; readonly edits: SectionEdits }

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

function decisionMessage(kind: FailureKind): string {
  return t(DECISION_MESSAGES[kind] ?? 'summary.decisionFailed.error')
}

function personName(reference: string | null | undefined, me: StaffMember): string | undefined {
  if (!reference || sameReference(reference, me.reference)) {
    return undefined
  }
  return findStaffByReference(reference)?.name ?? t('summary.anotherClinician')
}

function when(value: string | null | undefined): string {
  const date = parseInstant(value)
  return date ? formatDateTime(date) : ''
}

function Sources({ ids }: { readonly ids: readonly string[] }) {
  if (ids.length === 0) {
    return null
  }
  return (
    <ul className="srcs" aria-label={t('summary.sources')}>
      {ids.map((id) => (
        <li key={id} title={id}>
          <Icon name="doc" />
          {factLabel(id)}
        </li>
      ))}
    </ul>
  )
}

interface SectionBlockProps {
  readonly section: SummarySection
  readonly index: number
  readonly editing: boolean
  readonly onEdit: (index: number, text: string) => void
  readonly fieldRef?: Ref<HTMLTextAreaElement>
}

function SectionBlock({ section, index, editing, onEdit, fieldRef }: SectionBlockProps) {
  const fieldId = useId()
  const assessment = isAssessmentSection(section)
  if (editing && !assessment) {
    return (
      <div>
        <label htmlFor={fieldId}>{section.heading}</label>
        <textarea
          ref={fieldRef}
          id={fieldId}
          className="textarea"
          value={section.text}
          rows={Math.max(3, Math.ceil(section.text.length / 70))}
          onChange={(event) => onEdit(index, event.target.value)}
        />
        <Sources ids={section.sourceFactIds} />
      </div>
    )
  }
  return (
    <div>
      <h3>{section.heading}</h3>
      <p className="section-text">{section.text}</p>
      {assessment ? <p className="note">{t('summary.assessmentNote')}</p> : <Sources ids={section.sourceFactIds} />}
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
  readonly detail: CaseDetail
  readonly accessReason?: string
  readonly initial: HandoffSummary
  readonly initialEdits: SectionEdits
  readonly me: StaffMember
}

function DraftView({ detail, accessReason, initial, initialEdits, me }: DraftViewProps) {
  const caseId = detail.caseId
  const [summary, setSummary] = useState(initial)
  const [edits, setEdits] = useState<SectionEdits>(initialEdits)
  const [editing, setEditing] = useState(false)
  const [dialog, setDialog] = useState<'approve' | 'reject'>()
  const [reason, setReason] = useState('')
  const [reasonMissing, setReasonMissing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [decisionError, setDecisionError] = useState<FailureKind>()
  const [toast, setToast] = useState<string>()
  const ids = useId()
  const headingRef = useRef<HTMLHeadingElement>(null)
  const firstFieldRef = useRef<HTMLTextAreaElement>(null)
  const reasonRef = useRef<HTMLTextAreaElement>(null)
  const barRef = useRef<HTMLDivElement>(null)
  const open = summary.status === 'Draft'

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  useEffect(() => {
    if (editing) {
      firstFieldRef.current?.focus()
    }
  }, [editing])

  useEffect(() => {
    if (!open) {
      return undefined
    }
    // Browsers apply the page's scroll padding when they scroll a field into view, but not always
    // when a focused field is already on screen; this keeps it clear of the action bar either way.
    const keepClear = (event: FocusEvent): void => {
      const target = event.target
      if (!(target instanceof HTMLElement) || target.closest('dialog, .actionbar, .toast-region')) {
        return
      }
      window.requestAnimationFrame(() => {
        const bar = barRef.current
        if (bar && target.getBoundingClientRect().bottom > bar.getBoundingClientRect().top) {
          target.scrollIntoView({ block: 'nearest' })
        }
      })
    }
    document.addEventListener('focusin', keepClear)
    return () => document.removeEventListener('focusin', keepClear)
  }, [open])

  const dismissToast = useCallback(() => setToast(undefined), [])

  const sections = applyEdits(summary.sections, edits)
  const edited = hasEdits(summary.sections, edits)
  const facts = summary.missingOrConflictingFacts
  const firstEditable = sections.findIndex((section) => !isAssessmentSection(section))

  const onEdit = (index: number, text: string): void => {
    const next = { ...edits, [index]: text }
    setEdits(next)
    keepOpenDraft(caseId, { reviewerId: me.id, summary, edits: next })
  }

  const openDialog = (which: 'approve' | 'reject'): void => {
    setDecisionError(undefined)
    setReasonMissing(false)
    setDialog(which)
  }

  const closeDialog = (): void => {
    setDialog(undefined)
    setDecisionError(undefined)
    setReasonMissing(false)
  }

  const decided = (next: HandoffSummary, message: string): void => {
    forgetOpenDraft(caseId)
    setSummary(next)
    setEditing(false)
    setDialog(undefined)
    setToast(message)
  }

  const refused = (failure: ApiFailure): void => {
    if (failure.kind === 'conflict' || failure.kind === 'notFound') {
      // The draft was decided or replaced elsewhere, so coming back must offer a new one.
      forgetOpenDraft(caseId)
    }
    setDecisionError(failure.kind)
  }

  const approve = async (): Promise<void> => {
    setBusy(true)
    setDecisionError(undefined)
    const outcome = await approveHandoffSummary(caseId, summary.summaryId, approvedTextFor(summary, edits))
    setBusy(false)
    if (outcome.ok) {
      decided(outcome.data, t('summary.approvedToast'))
    } else {
      refused(outcome.failure)
    }
  }

  const reject = async (): Promise<void> => {
    if (reason.trim() === '') {
      setReasonMissing(true)
      reasonRef.current?.focus()
      return
    }
    setBusy(true)
    setDecisionError(undefined)
    const outcome = await rejectHandoffSummary(caseId, summary.summaryId, reason.trim())
    setBusy(false)
    if (outcome.ok) {
      decided(outcome.data, t('summary.rejectedToast'))
    } else {
      refused(outcome.failure)
    }
  }

  return (
    <>
      <div className="summary-grid">
        <section className="card" aria-labelledby={`${ids}-heading`}>
          <h2 className="card-title" id={`${ids}-heading`} tabIndex={-1} ref={headingRef}>
            {t('summary.cardTitle')}
            <span className="r pills">
              {edited ? <Badge tone="blue">{t('summary.editedByYou')}</Badge> : null}
              <span className="ai">
                <Icon name="spark" />
                {t('summary.aiDraft')}
              </span>
            </span>
          </h2>
          <Decision summary={summary} me={me} />
          <div className="sbar">
            {sections.map((section, index) => (
              <SectionBlock
                key={`${index}-${section.heading}`}
                section={section}
                index={index}
                editing={editing && open}
                onEdit={onEdit}
                fieldRef={index === firstEditable ? firstFieldRef : undefined}
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
                    <span className="mk" aria-hidden="true" />
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
          <OtherScreeningCard detail={detail} reason={accessReason} />
        </div>
      </div>

      {open ? (
        <div className="actionbar" role="group" aria-label={t('summary.actionsLabel')} ref={barRef}>
          <div className="btns">
            <button type="button" className="btn" onClick={() => openDialog('reject')}>
              {t('summary.reject')}
            </button>
            <button type="button" className="btn" onClick={() => setEditing((value) => !value)}>
              {editing ? t('summary.stopEditing') : t('summary.edit')}
            </button>
            <button type="button" className="btn primary" onClick={() => openDialog('approve')}>
              {t('summary.approve')}
            </button>
          </div>
        </div>
      ) : null}

      <Dialog open={dialog === 'approve'} title={t('summary.approveDialogTitle')} onClose={closeDialog}>
        <p>{t('summary.approveDialogBody')}</p>
        {decisionError ? (
          <p className="field-error" role="alert">
            {decisionMessage(decisionError)}
          </p>
        ) : null}
        <div className="dialog-actions">
          <button type="button" className="btn" onClick={closeDialog}>
            {t('summary.cancel')}
          </button>
          <button type="button" className="btn primary" disabled={busy} onClick={() => void approve()}>
            {busy ? t('summary.approving') : t('summary.approve')}
          </button>
        </div>
      </Dialog>

      <Dialog open={dialog === 'reject'} title={t('summary.rejectDialogTitle')} onClose={closeDialog}>
        <label htmlFor={`${ids}-reason`}>{t('summary.rejectReasonLabel')}</label>
        <p className="help" id={`${ids}-reason-help`}>
          {t('summary.rejectHelp')}
        </p>
        <textarea
          ref={reasonRef}
          id={`${ids}-reason`}
          className="textarea"
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
        {decisionError ? (
          <p className="field-error" role="alert">
            {decisionMessage(decisionError)}
          </p>
        ) : null}
        <div className="dialog-actions">
          <button type="button" className="btn" onClick={closeDialog}>
            {t('summary.cancel')}
          </button>
          <button type="button" className="btn primary" disabled={busy} onClick={() => void reject()}>
            {busy ? t('summary.rejecting') : t('summary.rejectConfirm')}
          </button>
        </div>
      </Dialog>

      <Toast message={toast} onDismiss={dismissToast} />
    </>
  )
}

interface WorkspaceProps {
  readonly detail: CaseDetail
  readonly reason?: string
}

function SummaryWorkspace({ detail, reason }: WorkspaceProps) {
  const me = useSignedInStaff()
  const [draft, setDraft] = useState<DraftState>(() => {
    const kept = openDraftFor(detail.caseId)
    return kept ? { step: 'ready', summary: kept.summary, edits: kept.edits } : { step: 'idle' }
  })
  const ids = useId()

  const requestDraft = async (): Promise<void> => {
    setDraft({ step: 'drafting' })
    const outcome = await draftHandoffSummary(detail.caseId)
    if (outcome.ok) {
      keepOpenDraft(detail.caseId, { reviewerId: me.id, summary: outcome.data, edits: {} })
      setDraft({ step: 'ready', summary: outcome.data, edits: {} })
    } else {
      setDraft({ step: 'failed', failure: outcome.failure })
    }
  }

  const drafted = draft.step === 'ready' ? parseInstant(draft.summary.draftedAt) : undefined
  const owner = describeOwner(detail.owner)
  const ownerIsMe = !detail.owner.isQueue && sameReference(detail.owner.reference, me.reference)
  const patient = describePatient(detail, me.role)

  return (
    <>
      <div className="page-head">
        <h1>{t('summary.title')}</h1>
        <p className="lede">
          {drafted ? t('summary.draftedAt', { date: formatDateTime(drafted) }) : t('summary.subtitle')}
        </p>
      </div>
      <div className="patient-head compact">
        <span className="pi" aria-hidden="true">
          <Icon name="user" />
        </span>
        <div className="who">
          <p className="nm" title={patient.reference}>
            {patient.name}
          </p>
          <p className="meta">
            {patient.identifier && !patient.hidden ? <span>{patient.identifier}</span> : null}
            <span title={detail.pathwayId}>{programmeName(detail.pathwayId)}</span>
            <span title={detail.sourceSystem}>{t('case.resultFrom', { result: detail.sourceResultId })}</span>
          </p>
        </div>
      </div>

      {!isClinicianRole(me.role) ? (
        <NotAvailableState
          icon="lock"
          title={t('summary.clinicianOnlyTitle')}
          body={ownerIsMe ? t('summary.clinicianOnlyYou') : t('summary.clinicianOnly', { name: owner.subject })}
        />
      ) : draft.step === 'ready' ? (
        <DraftView
          key={draft.summary.summaryId}
          detail={detail}
          accessReason={reason}
          initial={draft.summary}
          initialEdits={draft.edits}
          me={me}
        />
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
            <button type="button" className="btn linkrow" onClick={() => void requestDraft()}>
              {draft.failure.kind === 'notAvailable' ? t('states.checkAgain') : t('states.tryAgain')}
            </button>
          )}
        </section>
      ) : (
        <section className="card" aria-labelledby={`${ids}-start`}>
          <h2 className="card-title" id={`${ids}-start`}>
            {t('summary.startTitle')}
          </h2>
          <p className="why">{t('summary.startBody')}</p>
          <button
            type="button"
            className="btn primary linkrow"
            disabled={draft.step === 'drafting'}
            onClick={() => void requestDraft()}
          >
            <Icon name="spark" />
            {draft.step === 'drafting' ? t('summary.drafting') : t('summary.draft')}
          </button>
          {draft.step === 'drafting' ? (
            <div className="linkrow">
              <LoadingState label={t('summary.draftingLabel')} />
            </div>
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
 * they approved. AI never writes the assessment, so that section cannot be edited here. Approving
 * asks for a confirmation and rejecting asks for a reason, each in a small dialog, and a short
 * message confirms the decision. The actions stay in reach in a bar at the bottom of the screen;
 * the page keeps room above it so a focused field is never hidden. An open draft and its edits are
 * kept in memory while the reviewer looks at other screens, until they approve or reject it or
 * someone else signs in; the API cannot send a draft back, and clinical text is never written to
 * browser storage, so reloading the page loses it. When there is no draft (AI off, not built yet,
 * or failed), the screen says so plainly and never shows half-made text. Only clinician roles get
 * the actions; the API decides who may approve.
 */
export function SummaryPage({ caseId }: SummaryPageProps) {
  useDocumentTitle(t('summary.title'))
  const me = useSignedInStaff()
  const reason = useAccessReason(caseId)
  const read = useCase(caseId, reason)
  const askReason =
    read.state.status === 'failed' && read.state.failure.kind === 'forbidden' && !reason && me.facilityId !== undefined
  return (
    <div className="page">
      <a className="crumb" href={routeHref({ name: 'case', caseId })}>
        <Icon name="left" />
        {read.state.status === 'ready' ? describePatient(read.state.data, me.role).name : t('case.titleFallback')}
      </a>
      {read.state.status === 'ready' ? null : <h1 className="visually-hidden">{t('summary.title')}</h1>}
      {askReason ? (
        <OutsideTeamCard
          onReason={(text) => rememberAccessReason(caseId, text)}
          onCancel={() => navigate({ name: 'worklist' })}
        />
      ) : (
        <ReadView
          read={read}
          loadingLabel={t('case.loading')}
          skeleton="page"
          notAvailable={t('case.notAvailable')}
          errorTitle={t('case.errorTitle')}
          notFound={{ title: t('case.notFoundTitle'), body: t('case.notFoundBody') }}
          forbidden={{
            title: t('case.forbiddenTitle'),
            body: me.role === 'coordinator' ? t('case.forbiddenCoordinator') : t('case.forbiddenBody'),
          }}
        >
          {(detail) => <SummaryWorkspace detail={detail} reason={reason} />}
        </ReadView>
      )}
    </div>
  )
}
