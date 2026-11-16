import { useCallback, useId, useState } from 'react'
import { Badge, Dialog, Icon, OutsideTeamCard, ReadView, Toast } from '../../components'
import {
  acknowledgeEscalation,
  escalationsChanged,
  useCase,
  useCaseTimeline,
  type CaseDetail,
  type FailureKind,
  type Read,
  type TimelineEntry,
} from '../../data'
import {
  canAcknowledge,
  caseStatus,
  currentEscalation,
  describeOwner,
  describePatient,
  escalationTarget,
  isAddressedTo,
  nextActionText,
  nextActionWhy,
  noticeActor,
  programmeName,
} from '../../domain'
import { describeDue, formatWhen, parseInstant } from '../../format'
import { useDocumentTitle, useNow } from '../../hooks'
import { isEscalationRole, rememberAccessReason, roleLabel, useAccessReason, useSignedInStaff } from '../../identity'
import { t, type MessageKey } from '../../i18n'
import { navigate, routeHref } from '../../routing'
import { EscalationLadder } from './EscalationLadder'
import { NoticeHistory } from './NoticeHistory'

/** Props for {@link EscalationPage}. */
export interface EscalationPageProps {
  /** CareLoop's identifier of the escalated case, from the address. */
  readonly caseId: string
}

const ACK_FAILURES: Readonly<Partial<Record<FailureKind, MessageKey>>> = {
  conflict: 'escalation.ackFailed.conflict',
  forbidden: 'escalation.ackFailed.forbidden',
  notAvailable: 'escalation.ackFailed.notAvailable',
  notFound: 'escalation.ackFailed.notFound',
}

interface EscalationViewProps {
  readonly detail: CaseDetail
  readonly timeline: Read<TimelineEntry[]>
  readonly reason?: string
  readonly now: Date
  readonly onChanged: (detail: CaseDetail) => void
  readonly onStale: () => void
}

function EscalationView({ detail, timeline, reason, now, onChanged, onStale }: EscalationViewProps) {
  const me = useSignedInStaff()
  const ids = useId()
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<FailureKind>()
  const [toast, setToast] = useState<string>()
  const dismissToast = useCallback(() => setToast(undefined), [])

  const current = currentEscalation(detail.escalations)
  const status = caseStatus(detail)
  const owner = describeOwner(detail.owner)
  const dueAt = parseInstant(detail.dueAt)
  const due = dueAt ? describeDue(dueAt, now) : undefined
  const raised = parseInstant(current?.raisedAt)
  const acknowledged = parseInstant(current?.acknowledgedAt)
  const mayAcknowledge = canAcknowledge(detail, me)
  const entries = timeline.state.status === 'ready' ? timeline.state.data : undefined
  const addressedRole = current ? (roleLabel(current.escalatedTo.role) ?? current.escalatedTo.role) : undefined

  const close = (): void => {
    const stale = failure === 'conflict'
    setConfirming(false)
    setFailure(undefined)
    if (stale) {
      onStale()
    }
  }

  const acknowledge = async (): Promise<void> => {
    if (!current) {
      return
    }
    setBusy(true)
    setFailure(undefined)
    const outcome = await acknowledgeEscalation(detail.caseId, current.escalationId, reason)
    setBusy(false)
    if (outcome.ok) {
      setConfirming(false)
      onChanged(outcome.data)
      escalationsChanged()
      setToast(t('escalation.acknowledgedToast'))
      return
    }
    setFailure(outcome.failure.kind)
    if (outcome.failure.kind === 'conflict') {
      escalationsChanged()
    }
  }

  return (
    <>
      <div className="esc-grid">
        <section className="card hero" aria-labelledby={`${ids}-title`}>
          <div className="hero-top">
            <span className="lbl">{t('escalation.label')}</span>
            <span className="pills">
              <Badge tone={status.tone}>{status.label}</Badge>
              {current ? (
                <Badge tone="grey">{t('escalation.levelBadge', { level: Number(current.level) })}</Badge>
              ) : null}
            </span>
          </div>
          <h2 className="hero-title" id={`${ids}-title`}>
            {nextActionText(detail.nextAction)}
          </h2>
          <p className="why">{nextActionWhy(detail)}</p>
          <dl className="facts">
            <div>
              <dt>{t('case.responsible')}</dt>
              <dd title={owner.reference}>
                <span className="fact-main">{owner.name}</span>
                {owner.detail ? <span className="fact-sub">{owner.detail}</span> : null}
              </dd>
            </div>
            <div>
              <dt>{due?.overdue ? t('escalation.wasDue') : t('case.due')}</dt>
              <dd>
                {dueAt && due ? (
                  <>
                    <span className={due.overdue ? 'fact-main red' : 'fact-main'}>{formatWhen(dueAt, now)}</span>
                    <span className="fact-sub">{due.overdue ? due.primary : due.secondary}</span>
                  </>
                ) : (
                  <span className="fact-main">{t('time.noDueTime')}</span>
                )}
              </dd>
            </div>
            <div className="fact-wide">
              <dt>{t('escalation.escalatedTo')}</dt>
              <dd>
                {current ? (
                  <>
                    <span className="fact-main">{escalationTarget(current.escalatedTo).name}</span>
                    <span className="fact-sub">
                      {raised ? t('escalation.since', { date: formatWhen(raised, now) }) : null}
                    </span>
                  </>
                ) : (
                  <span className="fact-main">{t('escalation.noneOpen')}</span>
                )}
              </dd>
            </div>
          </dl>
          <EscalationLadder detail={detail} timeline={entries} now={now} />
        </section>

        <section className="card" aria-labelledby={`${ids}-ack`}>
          <h2 className="card-title" id={`${ids}-ack`}>
            {t('escalation.ackTitle')}
          </h2>
          <p className="why">{t('escalation.ackMeaning', { owner: owner.subject })}</p>
          {current && acknowledged ? (
            <div className="approved" role="status">
              <Icon name="check" />
              <div className="decision-text">
                {t('escalation.ackDone', {
                  name: noticeActor(
                    { reference: current.acknowledgedBy ?? '', role: current.acknowledgedByRole ?? current.escalatedTo.role },
                    me.reference,
                  ),
                  date: formatWhen(acknowledged, now),
                })}
                <span>{t('escalation.ackDoneNext')}</span>
              </div>
            </div>
          ) : null}
          {mayAcknowledge ? (
            <div className="hero-cta">
              <button type="button" className="btn primary" onClick={() => setConfirming(true)}>
                {t('escalation.acknowledge')}
              </button>
            </div>
          ) : current && !acknowledged && addressedRole ? (
            <p className="who-acts">
              <Icon name="user" />
              <span>
                {isAddressedTo(current.escalatedTo, me)
                  ? t('escalation.ackClosed')
                  : t('escalation.ackOnlyRole', { role: escalationTarget(current.escalatedTo).inSentence })}
              </span>
            </p>
          ) : null}
          <p className="note linkrow">{t('escalation.ackNote')}</p>
          <a className="btn linkrow" href={routeHref({ name: 'case', caseId: detail.caseId })}>
            {t('escalation.openCase')}
          </a>
        </section>
      </div>

      <NoticeHistory escalations={detail.escalations} timeline={entries} />

      <Dialog open={confirming} title={t('escalation.confirmTitle')} onClose={close}>
        <p>{t('escalation.confirmBody', { owner: owner.subject })}</p>
        {failure ? (
          <p className="field-error" role="alert">
            {t(ACK_FAILURES[failure] ?? 'escalation.ackFailed.error', { role: addressedRole ?? '' })}
          </p>
        ) : null}
        <div className="dialog-actions">
          <button type="button" className="btn" onClick={close}>
            {failure === 'conflict' ? t('escalation.close') : t('escalation.cancel')}
          </button>
          {failure === 'conflict' || failure === 'forbidden' ? null : (
            <button type="button" className="btn primary" disabled={busy} onClick={() => void acknowledge()}>
              {busy ? t('escalation.acknowledging') : t('escalation.acknowledge')}
            </button>
          )}
        </div>
      </Dialog>
      <Toast message={toast} onDismiss={dismissToast} />
    </>
  )
}

/**
 * Screen 17, an escalation: what is late and why, who is responsible, how late it is, the
 * escalation ladder with the current level, what happened, and, for the role the escalation went
 * to, a way to acknowledge it.
 *
 * @remarks
 * Reads `GET /cases/{caseId}` and its timeline, like the case screen, with the reason given for a
 * case outside the reader's team. "Acknowledge" shows only for the role the latest open escalation
 * went to, at the case's facility, and asks for a confirmation first: acknowledging records that
 * the person has seen it, and changes neither who is responsible nor the deadline, and does not
 * stop the clock. It posts `POST /cases/{caseId}/escalations/{escalationId}/acknowledgement` for
 * exactly the escalation on screen. If it was acknowledged or replaced meanwhile (409), or the role
 * may not (403), the dialog says so calmly and the screen reads the case again.
 */
export function EscalationPage({ caseId }: EscalationPageProps) {
  const me = useSignedInStaff()
  const reason = useAccessReason(caseId)
  const read = useCase(caseId, reason)
  const timeline = useCaseTimeline(caseId, reason)
  const now = useNow()
  const [updated, setUpdated] = useState<CaseDetail>()
  const ready = read.state.status === 'ready'
  const detail = updated ?? (read.state.status === 'ready' ? read.state.data : undefined)
  const patient = detail ? describePatient(detail, me.role) : undefined
  const current = detail ? currentEscalation(detail.escalations) : undefined
  const toMe = current ? isAddressedTo(current.escalatedTo, me) && !current.acknowledgedAt : false
  useDocumentTitle(patient ? t('escalation.documentTitle', { name: patient.name }) : t('escalation.title'))

  const refused =
    read.state.status === 'failed' && read.state.failure.kind === 'forbidden' && !reason && me.facilityId !== undefined

  const reread = (): void => {
    setUpdated(undefined)
    read.retry()
    timeline.retry()
  }

  return (
    <div className="page">
      {isEscalationRole(me.role) ? (
        <a className="crumb" href={routeHref({ name: 'escalations' })}>
          <Icon name="left" />
          {t('escalation.back')}
        </a>
      ) : (
        <a className="crumb" href={routeHref({ name: 'case', caseId })}>
          <Icon name="left" />
          {patient?.name ?? t('case.titleFallback')}
        </a>
      )}
      <div className="page-head">
        <h1>{toMe ? t('escalation.toYouTitle') : t('escalation.title')}</h1>
        {detail && patient ? (
          <p className="lede" title={patient.reference}>
            {[patient.name, patient.identifier, programmeName(detail.pathwayId)]
              .filter((part): part is string => Boolean(part))
              .join(t('common.separator'))}
          </p>
        ) : null}
      </div>
      {refused ? (
        <OutsideTeamCard
          onReason={(text) => rememberAccessReason(caseId, text)}
          onCancel={() => navigate({ name: isEscalationRole(me.role) ? 'escalations' : 'worklist' })}
        />
      ) : (
        <ReadView
          read={read}
          loadingLabel={t('escalation.loading')}
          skeleton="page"
          notAvailable={t('case.notAvailable')}
          errorTitle={t('escalation.errorTitle')}
          notFound={{ title: t('case.notFoundTitle'), body: t('case.notFoundBody') }}
          forbidden={{
            title: t('case.forbiddenTitle'),
            body: me.role === 'coordinator' ? t('case.forbiddenCoordinator') : t('case.forbiddenBody'),
          }}
        >
          {(data) =>
            ready ? (
              <EscalationView
                detail={updated ?? data}
                timeline={timeline}
                reason={reason}
                now={now}
                onChanged={(next) => {
                  setUpdated(next)
                  timeline.retry()
                }}
                onStale={reread}
              />
            ) : null
          }
        </ReadView>
      )}
    </div>
  )
}
