import { useId, useMemo, useState, type MouseEvent } from 'react'
import { Badge, EmptyState, Icon, ReadView } from '../../components'
import { useCases, type CaseSummary } from '../../data'
import { describeOwner, needsAttention, nextActionText } from '../../domain'
import { describeDue, formatLongDateTime, formatWhen, partOfDay } from '../../format'
import { ROOMY_LAYOUT_QUERY, WIDE_LAYOUT_QUERY, useDocumentTitle, useMediaQuery, useNow } from '../../hooks'
import { useSignedInStaff } from '../../identity'
import { t, tp } from '../../i18n'
import { navigate, routeHref } from '../../routing'
import {
  FILTER_TONES,
  WORKLIST_FILTERS,
  countByFilter,
  countOpen,
  groupRows,
  matchesFilter,
  matchesProgramme,
  matchesSearch,
  nextDeadline,
  programmeOptions,
  toWorklistRows,
  type WorklistCounts,
  type WorklistFilter,
  type WorklistRow,
  type WorklistSection,
} from './worklist'

function summaryLine(counts: WorklistCounts): string {
  const parts = [tp('worklist.summary.open', counts.open)]
  if (counts.overdue > 0) {
    parts.push(t('worklist.summary.overdue', { count: counts.overdue }))
  }
  if (counts.dueToday > 0) {
    parts.push(t('worklist.summary.dueToday', { count: counts.dueToday }))
  }
  return parts.join(t('common.separator'))
}

interface GreetingProps {
  readonly now: Date
  readonly summary?: string
}

function Greeting({ now, summary }: GreetingProps) {
  const me = useSignedInStaff()
  return (
    <div className="page-head">
      <p className="eyebrow">{formatLongDateTime(now)}</p>
      <h1>
        <span className="visually-hidden">{t('worklist.headingPrefix')}</span>
        {t(`worklist.greeting.${partOfDay(now)}`, { name: me.shortName })}
      </h1>
      {summary ? <p className="lede">{summary}</p> : null}
    </div>
  )
}

interface RowProps {
  readonly row: WorklistRow
  readonly now: Date
}

function StatusPills({ row }: { readonly row: WorklistRow }) {
  return (
    <span className="pills">
      <Badge tone={row.status.tone}>{row.status.label}</Badge>
      {row.summary.isEscalated ? <Badge tone="grey">{t('status.escalated')}</Badge> : null}
    </span>
  )
}

function DueLine({ row, now }: RowProps) {
  if (!row.due) {
    return <span className="muted">{t('time.noDueTime')}</span>
  }
  const due = describeDue(row.due, now)
  return (
    <>
      <span className={due.overdue ? 'red strong' : undefined}>{due.primary}</span>
      {t('common.separator')}
      {due.secondary}
    </>
  )
}

function CaseCard({ row, now }: RowProps) {
  const { summary } = row
  const owner = describeOwner(summary.owner, 'short')
  const ids = useId()
  return (
    <li className="case-card">
      <div className="cc-top">
        <h3 className="cc-patient">
          <span className="pi" aria-hidden="true">
            <Icon name="user" />
          </span>
          <a
            className="cc-link"
            href={routeHref({ name: 'case', caseId: summary.caseId })}
            title={summary.patientReference}
            aria-describedby={`${ids}-action ${ids}-due`}
          >
            {row.patient}
          </a>
        </h3>
        <StatusPills row={row} />
      </div>
      <p className="cc-action" id={`${ids}-action`}>
        {nextActionText(summary.nextAction)}
      </p>
      {row.reason ? <p className="cc-reason">{row.reason}</p> : null}
      <dl className="cc-facts">
        <div>
          <dt>{t('worklist.columns.due')}</dt>
          <dd id={`${ids}-due`}>
            <DueLine row={row} now={now} />
          </dd>
        </div>
        <div>
          <dt>{t('worklist.columns.responsible')}</dt>
          <dd title={owner.reference}>{owner.name}</dd>
        </div>
      </dl>
      <p className="cc-meta">
        <span>{[row.programme, row.stage].join(t('common.separator'))}</span>
        <Icon name="right" />
      </p>
    </li>
  )
}

function openCase(event: MouseEvent<HTMLTableRowElement>, caseId: string): void {
  if ((event.target as HTMLElement).closest('a, button') || window.getSelection()?.toString()) {
    return
  }
  navigate({ name: 'case', caseId })
}

interface CaseRowProps extends RowProps {
  readonly roomy: boolean
}

function CaseRow({ row, now, roomy }: CaseRowProps) {
  const { summary } = row
  const owner = describeOwner(summary.owner, 'short')
  const due = row.due ? describeDue(row.due, now) : undefined
  return (
    <tr className="row-link" onClick={(event) => openCase(event, summary.caseId)}>
      <td>
        <a className="name" href={routeHref({ name: 'case', caseId: summary.caseId })} title={summary.patientReference}>
          {row.patient}
        </a>
        {roomy ? null : <StatusPills row={row} />}
        <span className="l2">{[row.programme, row.stage].join(t('common.separator'))}</span>
      </td>
      <td>
        <span className="action">{nextActionText(summary.nextAction)}</span>
        {row.reason ? <span className="l2">{row.reason}</span> : null}
      </td>
      <td title={owner.reference}>
        {owner.name}
        {owner.detail ? <span className="l2">{owner.detail}</span> : null}
      </td>
      <td>
        {due ? (
          <>
            <span className={due.overdue ? 'red strong' : undefined}>{due.primary}</span>
            <span className="l2">{due.secondary}</span>
          </>
        ) : (
          <span className="muted">{t('time.noDueTime')}</span>
        )}
      </td>
      {roomy ? (
        <td>
          <StatusPills row={row} />
        </td>
      ) : null}
    </tr>
  )
}

interface GroupProps {
  readonly section: WorklistSection
  readonly wide: boolean
  readonly roomy: boolean
  readonly now: Date
}

function Group({ section, wide, roomy, now }: GroupProps) {
  const headingId = useId()
  const name = t(`worklist.groups.${section.group}`)
  const count = section.rows.length
  return (
    <section className="group" aria-labelledby={headingId}>
      <h2 className="group-title" id={headingId}>
        {name}{' '}
        <span className="count">
          <span aria-hidden="true">{count}</span>
          <span className="visually-hidden">{tp('worklist.groupCount', count)}</span>
        </span>
      </h2>
      {wide ? (
        <div className="tablecard">
          <table className={roomy ? 'worklist' : 'worklist four'}>
            <caption className="visually-hidden">{t('worklist.caption', { group: name })}</caption>
            <colgroup>
              <col className="c-patient" />
              <col />
              <col className="c-responsible" />
              <col className="c-due" />
              {roomy ? <col className="c-status" /> : null}
            </colgroup>
            <thead>
              <tr>
                <th scope="col">{t('worklist.columns.patient')}</th>
                <th scope="col">{t('worklist.columns.nextAction')}</th>
                <th scope="col">{t('worklist.columns.responsible')}</th>
                <th scope="col">{t('worklist.columns.due')}</th>
                {roomy ? <th scope="col">{t('worklist.columns.status')}</th> : null}
              </tr>
            </thead>
            <tbody>
              {section.rows.map((row) => (
                <CaseRow key={row.summary.caseId} row={row} now={now} roomy={roomy} />
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <ul className="case-list">
          {section.rows.map((row) => (
            <CaseCard key={row.summary.caseId} row={row} now={now} />
          ))}
        </ul>
      )}
    </section>
  )
}

interface WorklistBodyProps {
  readonly cases: readonly CaseSummary[]
  readonly now: Date
}

function WorklistBody({ cases, now }: WorklistBodyProps) {
  const wide = useMediaQuery(WIDE_LAYOUT_QUERY)
  const roomy = useMediaQuery(ROOMY_LAYOUT_QUERY)
  const rows = useMemo(() => toWorklistRows(cases), [cases])
  const [filter, setFilter] = useState<WorklistFilter>('all')
  const [programme, setProgramme] = useState('')
  const [query, setQuery] = useState('')
  const [showAll, setShowAll] = useState(false)
  const ids = useId()

  const programmes = useMemo(() => programmeOptions(rows), [rows])
  const scoped = rows.filter((row) => matchesProgramme(row, programme) && matchesSearch(row, query))
  const counts = countByFilter(scoped)
  const visible = scoped.filter((row) => matchesFilter(row, filter))
  const sections = groupRows(visible, now)
  const allOnTrack = !rows.some((row) => needsAttention(row.status))
  const summary = summaryLine(countOpen(rows, now))

  const clearFilters = (): void => {
    setFilter('all')
    setProgramme('')
    setQuery('')
  }

  if (rows.length === 0 || (allOnTrack && !showAll)) {
    const deadline = nextDeadline(rows)
    const body =
      rows.length === 0
        ? t('worklist.emptyNoCases')
        : deadline
          ? tp('worklist.emptyAllOnTrack', rows.length, { date: formatWhen(deadline, now) })
          : tp('worklist.emptyAllOnTrackNoDeadline', rows.length)
    return (
      <>
        <Greeting now={now} summary={summary} />
        <EmptyState
          icon="positive"
          title={t('worklist.emptyTitle')}
          body={body}
          action={
            rows.length > 0 ? (
              <button type="button" className="btn" onClick={() => setShowAll(true)}>
                {t('worklist.showAll')}
              </button>
            ) : undefined
          }
        />
      </>
    )
  }

  return (
    <>
      <Greeting now={now} summary={summary} />
      <div className="toolbar">
        <label className="field search" htmlFor={`${ids}-search`}>
          <Icon name="search" />
          <span className="visually-hidden">{t('worklist.searchLabel')}</span>
          <input
            id={`${ids}-search`}
            type="search"
            value={query}
            placeholder={t('worklist.searchPlaceholder')}
            autoComplete="off"
            onChange={(event) => setQuery(event.target.value)}
          />
        </label>
        <label className="field choose" htmlFor={`${ids}-programme`}>
          <span className="visually-hidden">{t('worklist.programmeLabel')}</span>
          <select id={`${ids}-programme`} value={programme} onChange={(event) => setProgramme(event.target.value)}>
            <option value="">{t('worklist.allProgrammes')}</option>
            {programmes.map((option) => (
              <option key={option.id} value={option.id}>
                {option.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="chips" role="group" aria-label={t('worklist.filtersLabel')}>
        {WORKLIST_FILTERS.map((id) => {
          const tone = FILTER_TONES[id]
          return (
            <button key={id} type="button" className="chip" aria-pressed={filter === id} onClick={() => setFilter(id)}>
              {tone ? <i className={`d ${tone}`} aria-hidden="true" /> : null}
              {t(`worklist.chips.${id}`)} <b>{counts[id]}</b>
            </button>
          )
        })}
      </div>

      <p className="visually-hidden" aria-live="polite">
        {t('worklist.showing', { shown: visible.length, total: rows.length })}
      </p>

      {sections.length === 0 ? (
        <EmptyState
          title={t('worklist.filterEmptyTitle')}
          body={t('worklist.filterEmptyBody')}
          action={
            <button type="button" className="btn" onClick={clearFilters}>
              {t('worklist.clearFilters')}
            </button>
          }
        />
      ) : (
        <div className="groups">
          {sections.map((section) => (
            <Group key={section.group} section={section} wide={wide} roomy={roomy} now={now} />
          ))}
        </div>
      )}
    </>
  )
}

/**
 * Screen 1, the worklist: a greeting and a one-line summary, then every open case grouped into
 * "Needs action now", "Due today" and "Coming up", each with one next action, one person
 * responsible and one due time.
 *
 * @remarks
 * Read-only; reads `GET /cases`. On wide screens each group is a table, with a separate status
 * column from 1200 px and the status under the patient below that; below 900 px each case is a card
 * that opens the case wherever it is tapped. The chips are the exception queues, and a case can
 * sit in more than one. When nothing needs attention it says so, with the next deadline, instead of
 * listing cases.
 */
export function WorklistPage() {
  useDocumentTitle(t('worklist.title'))
  const read = useCases()
  const now = useNow()
  return (
    <div className="page">
      {read.state.status === 'ready' ? null : <Greeting now={now} />}
      <ReadView
        read={read}
        loadingLabel={t('worklist.loading')}
        skeleton="list"
        notAvailable={t('worklist.notAvailable')}
        errorTitle={t('worklist.errorTitle')}
      >
        {(cases) => <WorklistBody cases={cases} now={now} />}
      </ReadView>
      <p className="note">{t('worklist.note')}</p>
    </div>
  )
}
