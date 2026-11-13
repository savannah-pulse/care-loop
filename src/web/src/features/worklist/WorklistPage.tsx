import { useId, useMemo, useState, type MouseEvent, type ReactNode } from 'react'
import { EmptyState, Icon, ReadView, Reference, StatusText } from '../../components'
import { useCases, type CaseSummary } from '../../data'
import { describeOwner, needsAttention, nextActionText, stageLabel, stagePosition } from '../../domain'
import { describeDue, formatLongDateTime, formatWhen } from '../../format'
import { useDocumentTitle, useNow } from '../../hooks'
import { t, tp } from '../../i18n'
import { navigate, routeHref } from '../../routing'
import {
  FILTER_TONES,
  WORKLIST_FILTERS,
  countByFilter,
  matchesFilter,
  matchesProgramme,
  matchesSearch,
  nextDeadline,
  programmeOptions,
  sortByRisk,
  toWorklistRows,
  type WorklistFilter,
  type WorklistRow,
} from './worklist'

interface RowProps {
  readonly row: WorklistRow
  readonly now: Date
}

function openCase(event: MouseEvent<HTMLTableRowElement>, caseId: string): void {
  if ((event.target as HTMLElement).closest('a, button') || window.getSelection()?.toString()) {
    return
  }
  navigate({ name: 'case', caseId })
}

function WorklistTableRow({ row, now }: RowProps) {
  const { summary, status, due } = row
  const owner = describeOwner(summary.owner, 'short')
  const dueText = due ? describeDue(due, now) : undefined
  return (
    <tr className="row-link" onClick={(event) => openCase(event, summary.caseId)}>
      <td>
        <a className="name" href={routeHref({ name: 'case', caseId: summary.caseId })}>
          <Reference value={summary.patientReference} />
        </a>
        <span className="l2">{t('worklist.patientDetail')}</span>
      </td>
      <td>{row.programme}</td>
      <td>
        {stageLabel(summary.stage)}
        <span className="l2">{stagePosition(summary.stage)}</span>
      </td>
      <td>
        {nextActionText(summary.nextAction)}
        {row.otherFlags.length > 0 ? <span className="l2">{row.otherFlags.join(', ')}</span> : null}
      </td>
      <td>
        {owner.name}
        <span className="l2">{owner.detailIsReference ? <Reference value={owner.detail} /> : owner.detail}</span>
      </td>
      <td>
        {dueText ? (
          <>
            <span className={dueText.overdue ? 'red strong' : undefined}>{dueText.primary}</span>
            <span className="l2">{dueText.secondary}</span>
          </>
        ) : (
          <span className="muted">{t('time.noDueTime')}</span>
        )}
      </td>
      <td>
        <StatusText tone={status.tone}>{status.label}</StatusText>
        {status.detail ? <span className="l2">{status.detail}</span> : null}
      </td>
    </tr>
  )
}

interface WorklistBodyProps {
  readonly cases: readonly CaseSummary[]
  readonly now: Date
}

function WorklistBody({ cases, now }: WorklistBodyProps) {
  const rows = useMemo(() => toWorklistRows(cases), [cases])
  const [filter, setFilter] = useState<WorklistFilter>('all')
  const [programme, setProgramme] = useState('')
  const [query, setQuery] = useState('')
  const [showAll, setShowAll] = useState(false)
  const ids = useId()

  const programmes = useMemo(() => programmeOptions(rows), [rows])
  const scoped = rows.filter((row) => matchesProgramme(row, programme) && matchesSearch(row, query))
  const counts = countByFilter(scoped)
  const visible = sortByRisk(scoped.filter((row) => matchesFilter(row, filter)))
  const allOnTrack = !rows.some((row) => needsAttention(row.status))

  const subtitle = t('worklist.subtitle', {
    date: formatLongDateTime(now),
    count: tp('worklist.openCases', rows.length),
  })

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
        <WorklistHead subtitle={subtitle} />
        <EmptyState
          positive
          title={t('worklist.emptyTitle')}
          body={body}
          action={
            rows.length > 0 ? (
              <button type="button" className="linkbtn" onClick={() => setShowAll(true)}>
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
      <WorklistHead subtitle={subtitle}>
        <div className="filters">
          <label className="field" htmlFor={`${ids}-programme`}>
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
        </div>
      </WorklistHead>

      <div className="chips" role="group" aria-label={t('worklist.filtersLabel')}>
        {WORKLIST_FILTERS.map((id) => {
          const tone = FILTER_TONES[id]
          return (
            <button
              key={id}
              type="button"
              className="chip"
              aria-pressed={filter === id}
              onClick={() => setFilter(id)}
            >
              {tone ? <i className={`d ${tone}`} aria-hidden="true" /> : null}
              {t(`worklist.chips.${id}`)} <b>{counts[id]}</b>
            </button>
          )
        })}
      </div>

      <p className="visually-hidden" aria-live="polite">
        {t('worklist.showing', { shown: visible.length, total: rows.length })}
      </p>

      <div className="tablecard">
        {visible.length === 0 ? (
          <EmptyState
            title={t('worklist.filterEmptyTitle')}
            body={t('worklist.filterEmptyBody')}
            action={
              <button type="button" className="btn small-btn" onClick={clearFilters}>
                {t('worklist.clearFilters')}
              </button>
            }
          />
        ) : (
          <table className="worklist">
            <caption className="visually-hidden">{t('worklist.caption')}</caption>
            <colgroup>
              <col className="c-patient" />
              <col className="c-programme" />
              <col className="c-stage" />
              <col />
              <col className="c-responsible" />
              <col className="c-due" />
              <col className="c-status" />
            </colgroup>
            <thead>
              <tr>
                <th scope="col">{t('worklist.columns.patient')}</th>
                <th scope="col">{t('worklist.columns.programme')}</th>
                <th scope="col">{t('worklist.columns.stage')}</th>
                <th scope="col">{t('worklist.columns.nextAction')}</th>
                <th scope="col">{t('worklist.columns.responsible')}</th>
                <th scope="col">{t('worklist.columns.due')}</th>
                <th scope="col">{t('worklist.columns.status')}</th>
              </tr>
            </thead>
            <tbody>
              {visible.map((row) => (
                <WorklistTableRow key={row.summary.caseId} row={row} now={now} />
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}

interface WorklistHeadProps {
  readonly subtitle: string
  readonly children?: ReactNode
}

function WorklistHead({ subtitle, children }: WorklistHeadProps) {
  return (
    <div className="head">
      <div>
        <h1>{t('worklist.title')}</h1>
        <p className="sub">{subtitle}</p>
      </div>
      {children}
    </div>
  )
}

/**
 * Screen 1, the worklist: every open case, most at risk first, each with one next action, one
 * person responsible and one due time.
 *
 * @remarks
 * Read-only; reads `GET /cases`. The chips are the exception queues, and a case can sit in more
 * than one. When nothing needs attention it says so, with the next deadline, instead of showing a
 * table. Selecting a row opens the case.
 */
export function WorklistPage() {
  useDocumentTitle(t('worklist.title'))
  const read = useCases()
  const now = useNow()
  return (
    <div className="page">
      {read.state.status === 'ready' ? null : <WorklistHead subtitle={formatLongDateTime(now)} />}
      <ReadView
        read={read}
        loading={t('worklist.loading')}
        notAvailable={t('worklist.notAvailable')}
        errorTitle={t('worklist.errorTitle')}
      >
        {(cases) => <WorklistBody cases={cases} now={now} />}
      </ReadView>
      <p className="note">{t('worklist.note')}</p>
    </div>
  )
}
