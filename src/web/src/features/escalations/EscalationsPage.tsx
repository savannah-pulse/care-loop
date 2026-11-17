import { useId } from 'react'
import { Badge, EmptyState, Icon, ReadView } from '../../components'
import { useEscalatedCases, type EscalatedCase } from '../../data'
import {
  caseLinkNames,
  currentEscalation,
  describeOwner,
  describePatient,
  escalationTarget,
  isAddressedTo,
  nextActionText,
  noticeActor,
  programmeName,
  type Tone,
} from '../../domain'
import { describeDue, formatLongDateTime, formatWhen, parseInstant } from '../../format'
import { useDocumentTitle, useNow } from '../../hooks'
import { useSignedInStaff, type StaffMember } from '../../identity'
import { t, tp } from '../../i18n'
import { routeHref } from '../../routing'

/** Where one escalated case stands for the person reading the list. */
interface EscalationRow {
  readonly item: EscalatedCase
  readonly needsMe: boolean
  readonly tone: Tone
  readonly status: string
  readonly due?: Date
}

function toRow(item: EscalatedCase, me: StaffMember, now: Date): EscalationRow {
  const due = parseInstant(item.summary.dueAt)
  const current = item.detail ? currentEscalation(item.detail.escalations) : undefined
  if (!current) {
    return { item, needsMe: false, tone: 'grey', status: t('escalations.status.unknown'), due }
  }
  const acknowledged = parseInstant(current.acknowledgedAt)
  const toMe = isAddressedTo(current.escalatedTo, me)
  if (acknowledged) {
    const name = noticeActor(
      { reference: current.acknowledgedBy ?? '', role: current.acknowledgedByRole ?? current.escalatedTo.role },
      me.reference,
    )
    return {
      item,
      needsMe: false,
      tone: 'green',
      status: t('escalations.status.acknowledged', { name, date: formatWhen(acknowledged, now) }),
      due,
    }
  }
  if (toMe) {
    return { item, needsMe: true, tone: 'amber', status: t('escalations.status.needsYou'), due }
  }
  return {
    item,
    needsMe: false,
    tone: 'blue',
    status: t('escalations.status.with', { target: escalationTarget(current.escalatedTo).inSentence }),
    due,
  }
}

function sortRows(rows: readonly EscalationRow[]): EscalationRow[] {
  return [...rows].sort((a, b) => {
    if (a.needsMe !== b.needsMe) {
      return a.needsMe ? -1 : 1
    }
    return (a.due?.getTime() ?? Number.POSITIVE_INFINITY) - (b.due?.getTime() ?? Number.POSITIVE_INFINITY)
  })
}

interface EscalationCardProps {
  readonly row: EscalationRow
  readonly linkName: string
  readonly now: Date
}

function EscalationCard({ row, linkName, now }: EscalationCardProps) {
  const me = useSignedInStaff()
  const ids = useId()
  const { summary } = row.item
  const patient = describePatient(summary, me.role)
  const owner = describeOwner(summary.owner, 'short')
  const due = row.due ? describeDue(row.due, now) : undefined
  return (
    <li className="case-card">
      <div className="cc-top">
        <h3 className="cc-patient">
          <span className="pi" aria-hidden="true">
            <Icon name="alert" />
          </span>
          <span className="cc-who">
            <a
              className="cc-link"
              href={routeHref({ name: 'escalation', caseId: summary.caseId })}
              title={patient.reference}
              aria-label={linkName === patient.name ? undefined : linkName}
              aria-describedby={`${ids}-status ${ids}-action`}
            >
              {patient.name}
            </a>
            {patient.identifier ? (
              <span className="cc-id" aria-hidden={patient.hidden ? undefined : true}>
                {patient.identifier}
              </span>
            ) : null}
          </span>
        </h3>
        <span className="pills" id={`${ids}-status`}>
          <Badge tone={row.tone}>{row.status}</Badge>
        </span>
      </div>
      <p className="cc-action" id={`${ids}-action`}>
        {nextActionText(summary.nextAction)}
      </p>
      <dl className="cc-facts">
        <div>
          <dt>{t('worklist.columns.due')}</dt>
          <dd>
            {due ? (
              <>
                <span className={due.overdue ? 'red strong' : undefined}>{due.primary}</span>
                {t('common.separator')}
                {due.secondary}
              </>
            ) : (
              <span className="muted">{t('time.noDueTime')}</span>
            )}
          </dd>
        </div>
        <div>
          <dt>{t('worklist.columns.responsible')}</dt>
          <dd title={owner.reference}>{owner.name}</dd>
        </div>
      </dl>
      <p className="cc-meta">
        <span>{programmeName(summary.pathwayId)}</span>
        <Icon name="right" />
      </p>
    </li>
  )
}

function EscalationList({ items, now }: { readonly items: readonly EscalatedCase[]; readonly now: Date }) {
  const me = useSignedInStaff()
  const rows = sortRows(items.map((item) => toRow(item, me, now)))
  const linkNames = caseLinkNames(
    rows.map(({ item }) => ({
      caseId: item.summary.caseId,
      patient: describePatient(item.summary, me.role),
      programme: programmeName(item.summary.pathwayId),
    })),
  )
  const needMe = rows.filter((row) => row.needsMe).length
  if (rows.length === 0) {
    return (
      <>
        <Lede now={now} needMe={0} />
        <EmptyState
          icon="positive"
          title={t('escalations.emptyTitle')}
          body={me.role === 'coordinator' ? t('escalations.emptyBodyCoordinator') : t('escalations.emptyBody')}
        />
      </>
    )
  }
  return (
    <>
      <Lede now={now} needMe={needMe} />
      <ul className="case-list one">
        {rows.map((row, index) => (
          <EscalationCard key={row.item.summary.caseId} row={row} linkName={linkNames[index]} now={now} />
        ))}
      </ul>
    </>
  )
}

function Lede({ now, needMe }: { readonly now: Date; readonly needMe?: number }) {
  return (
    <p className="lede">
      {formatLongDateTime(now)}
      {needMe === undefined
        ? null
        : `${t('common.separator')}${needMe === 0 ? t('escalations.noneNeedYou') : tp('escalations.needYou', needMe)}`}
    </p>
  )
}

/**
 * Escalations: every open escalation the facility lead or programme coordinator can see, the ones
 * waiting for their acknowledgement first (screen 17).
 *
 * @remarks
 * Reads `GET /cases?escalated=true`, then each case for its escalation ladder. The facility lead
 * sees their facility's escalated cases; the programme coordinator sees the cases escalated to them,
 * without names. Each row says whether it waits for the reader, who acknowledged it, or who it is
 * with now, and opens the escalation; its link is named by the patient as shown, with the MRN, so
 * no two links read the same.
 */
export function EscalationsPage() {
  useDocumentTitle(t('escalations.title'))
  const read = useEscalatedCases()
  const now = useNow()
  return (
    <div className="page">
      <div className="page-head">
        <h1>{t('escalations.title')}</h1>
        {read.state.status === 'ready' ? null : <Lede now={now} />}
      </div>
      <ReadView
        read={read}
        loadingLabel={t('escalations.loading')}
        skeleton="list"
        notAvailable={t('escalations.notAvailable')}
        errorTitle={t('escalations.errorTitle')}
      >
        {(items) => <EscalationList items={items} now={now} />}
      </ReadView>
      <p className="note">{t('escalations.note')}</p>
    </div>
  )
}
