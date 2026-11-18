import { useEffect, useId, useMemo, useState, type MouseEvent, type ReactNode } from 'react'
import { AccessReasonForm, Badge, Dialog, EmptyState, Icon, ReadView } from '../../components'
import { CASE_LIST_LIMIT, useCaseList, type CaseList } from '../../data'
import { describeOwner, needsAttention, nextActionText } from '../../domain'
import { KNOWN_FACILITIES, describeDue, facilityName, formatLongDateTime, formatWhen, partOfDay } from '../../format'
import { ROOMY_LAYOUT_QUERY, WIDE_LAYOUT_QUERY, useDocumentTitle, useMediaQuery, useNow } from '../../hooks'
import { namesHiddenFor, rememberAccessReasons, useSignedInStaff } from '../../identity'
import { t, tp } from '../../i18n'
import { navigate, routeHref } from '../../routing'
import {
  FILTER_TONES,
  WORKLIST_FILTERS,
  WORKLIST_SCOPES,
  countByFilter,
  countOpen,
  groupRows,
  hasOwnWorklist,
  isMine,
  matchesFilter,
  matchesProgramme,
  matchesSearch,
  nextDeadline,
  programmeOptions,
  toWorklistRows,
  type WorklistCounts,
  type WorklistFilter,
  type WorklistRow,
  type WorklistScope,
  type WorklistSection,
} from './worklist'

/** Another team's cases the person chose to look at, and why. */
interface TeamScope {
  readonly facilityId: string
  readonly reason: string
}

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
  const { summary, patient } = row
  const owner = describeOwner(summary.owner, 'short')
  const ids = useId()
  return (
    <li className="case-card">
      <div className="cc-top">
        <h3 className="cc-patient">
          <span className="pi" aria-hidden="true">
            <Icon name="user" />
          </span>
          <span className="cc-who">
            <a
              className="cc-link"
              href={routeHref({ name: 'case', caseId: summary.caseId })}
              title={patient.reference}
              aria-label={row.linkName === patient.name ? undefined : row.linkName}
              aria-describedby={`${ids}-action ${ids}-due`}
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
  const { summary, patient } = row
  const owner = describeOwner(summary.owner, 'short')
  const due = row.due ? describeDue(row.due, now) : undefined
  return (
    <tr className="row-link" onClick={(event) => openCase(event, summary.caseId)}>
      <td>
        <a
          className="name"
          href={routeHref({ name: 'case', caseId: summary.caseId })}
          title={patient.reference}
          aria-label={row.linkName === patient.name ? undefined : row.linkName}
        >
          {patient.name}
        </a>
        {patient.identifier ? (
          <span className="l2" aria-hidden={patient.hidden ? undefined : true}>
            {patient.identifier}
          </span>
        ) : null}
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

interface TeamDialogProps {
  readonly open: boolean
  readonly ownFacility: string
  readonly onChoose: (scope: TeamScope) => void
  readonly onClose: () => void
}

function TeamDialog({ open, ownFacility, onChoose, onClose }: TeamDialogProps) {
  const ids = useId()
  const others = KNOWN_FACILITIES.filter((facility) => facility !== ownFacility)
  const [facilityId, setFacilityId] = useState<string>(others[0] ?? '')
  return (
    <Dialog open={open} title={t('worklist.team.dialogTitle')} onClose={onClose}>
      {open ? (
        <AccessReasonForm
          intro={t('worklist.team.dialogIntro')}
          before={
            <div className="reason-team">
              <label htmlFor={`${ids}-team`}>{t('worklist.team.chooseLabel')}</label>
              <span className="field choose">
                <select id={`${ids}-team`} value={facilityId} onChange={(event) => setFacilityId(event.target.value)}>
                  {others.map((facility) => (
                    <option key={facility} value={facility}>
                      {facilityName(facility)}
                    </option>
                  ))}
                </select>
              </span>
            </div>
          }
          options={[t('access.teamReasons.covering'), t('access.teamReasons.patientNeedsHelp')]}
          submitLabel={t('worklist.team.submit')}
          onSubmit={(reason) => onChoose({ facilityId, reason })}
          onCancel={onClose}
        />
      ) : null}
    </Dialog>
  )
}

interface WorklistBodyProps {
  readonly list: CaseList
  readonly now: Date
  readonly toolbarExtra?: ReactNode
  /** `true` while the list is the person's own team's, where "My cases" means something. */
  readonly ownTeam: boolean
}

function WorklistBody({ list, now, toolbarExtra, ownTeam }: WorklistBodyProps) {
  const me = useSignedInStaff()
  const wide = useMediaQuery(WIDE_LAYOUT_QUERY)
  const roomy = useMediaQuery(ROOMY_LAYOUT_QUERY)
  const teamRows = useMemo(() => toWorklistRows(list.cases, me.role), [list.cases, me.role])
  const mineRows = useMemo(() => teamRows.filter((row) => isMine(row, me)), [teamRows, me])
  const canNarrow = ownTeam && hasOwnWorklist(me.role)
  const [chosenScope, setChosenScope] = useState<WorklistScope>('mine')
  const scope: WorklistScope = canNarrow ? chosenScope : 'team'
  const rows = scope === 'mine' ? mineRows : teamRows
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
  const namesHidden = namesHiddenFor(me.role)
  const limitNote = list.complete ? null : (
    <p className="note">{t('worklist.limited', { limit: CASE_LIST_LIMIT })}</p>
  )

  const clearFilters = (): void => {
    setFilter('all')
    setProgramme('')
    setQuery('')
  }

  const scopeBar = canNarrow ? (
    <div className="chips" role="group" aria-label={t('worklist.scopeLabel')}>
      {WORKLIST_SCOPES.map((id) => (
        <button
          key={id}
          type="button"
          className="chip"
          aria-pressed={scope === id}
          onClick={() => {
            setChosenScope(id)
            setShowAll(false)
          }}
        >
          {t(`worklist.scopes.${id}`)} <b>{id === 'mine' ? mineRows.length : teamRows.length}</b>
        </button>
      ))}
    </div>
  ) : null

  // A person's own cases are always listed. Only the whole team's list is folded away while every
  // case on it is on track, where a long list of cases that need nothing would bury the message.
  if (rows.length === 0 || (allOnTrack && !showAll && scope === 'team')) {
    const deadline = nextDeadline(rows)
    const noneOfMine = rows.length === 0 && scope === 'mine' && teamRows.length > 0
    const body =
      rows.length === 0
        ? noneOfMine
          ? tp('worklist.emptyNoneOfMine', teamRows.length)
          : me.role === 'coordinator'
            ? t('worklist.emptyNothingEscalated')
            : t('worklist.emptyNoCases')
        : deadline
          ? tp('worklist.emptyAllOnTrack', rows.length, { date: formatWhen(deadline, now) })
          : tp('worklist.emptyAllOnTrackNoDeadline', rows.length)
    return (
      <>
        <Greeting now={now} summary={summary} />
        {toolbarExtra}
        {scopeBar}
        <EmptyState
          icon="positive"
          title={t('worklist.emptyTitle')}
          body={body}
          action={
            rows.length > 0 ? (
              <button type="button" className="btn" onClick={() => setShowAll(true)}>
                {t('worklist.showAll')}
              </button>
            ) : noneOfMine ? (
              <button type="button" className="btn" onClick={() => setChosenScope('team')}>
                {t('worklist.showTeam')}
              </button>
            ) : undefined
          }
        />
        {limitNote}
      </>
    )
  }

  return (
    <>
      <Greeting now={now} summary={summary} />
      {toolbarExtra}
      {scopeBar}
      <div className="toolbar">
        <label className="field search" htmlFor={`${ids}-search`}>
          <Icon name="search" />
          <span className="visually-hidden">
            {namesHidden ? t('worklist.searchCaseLabel') : t('worklist.searchLabel')}
          </span>
          <input
            id={`${ids}-search`}
            type="search"
            value={query}
            placeholder={namesHidden ? t('worklist.searchCasePlaceholder') : t('worklist.searchPlaceholder')}
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

      <p className="showing" aria-live="polite">
        {tp('worklist.showing', rows.length, { shown: visible.length })}
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
      {limitNote}
    </>
  )
}

interface TeamBarProps {
  readonly scope?: TeamScope
  readonly canChoose: boolean
  readonly onChoose: () => void
  readonly onBack: () => void
}

function TeamBar({ scope, canChoose, onChoose, onBack }: TeamBarProps) {
  const me = useSignedInStaff()
  if (scope) {
    return (
      <div className="notice team-bar" role="status">
        <Icon name="lock" />
        <span className="team-text">
          <b>{t('worklist.team.showing', { facility: facilityName(scope.facilityId) })}</b>
          <span>{t('worklist.team.reasonRecorded', { reason: scope.reason })}</span>
        </span>
        <button type="button" className="btn" onClick={onBack}>
          {t('worklist.team.back')}
        </button>
      </div>
    )
  }
  if (!canChoose || !me.facilityId) {
    return null
  }
  return (
    <p className="team-line">
      <span className="muted">{t('worklist.team.yours', { facility: facilityName(me.facilityId) })}</span>
      <button type="button" className="linkbtn" onClick={onChoose}>
        {t('worklist.team.choose')}
      </button>
    </p>
  )
}

/**
 * Screen 1, the worklist: a greeting and a one-line summary, then the open cases grouped into
 * "Needs action now", "Due today" and "Coming up", each with one next action, one person
 * responsible and one due time.
 *
 * It opens on "My cases", the cases that wait for the signed-in person (see `isMine`), and
 * "Whole team" shows every open case at their facility. The summary line and the counts follow
 * whichever is showing. The programme coordinator has no such choice: their list is already the
 * cases escalated to them.
 *
 * @remarks
 * Read-only; reads `GET /cases` page by page (up to {@link CASE_LIST_LIMIT} cases) and says how many
 * it shows. The API limits the list to the signed-in person's facility, and to the cases escalated
 * to them for the programme coordinator, whose list shows no names. Staff can look at another
 * team's cases after saying why; the reason goes with every read and with each case they open from
 * that list. On wide screens each group is a table, with a separate status column from 1200 px and
 * the status under the patient below that; below 900 px each case is a card that opens the case
 * wherever it is tapped. The chips are the exception queues, and a case can sit in more than one.
 * Each case's link is named by the patient as shown, with the MRN (the case number for the
 * coordinator), so no two links read the same to a screen reader.
 * When nothing on the team's list needs attention it says so, with the next deadline, instead of
 * listing cases; a person's own cases are always listed.
 */
export function WorklistPage() {
  useDocumentTitle(t('worklist.title'))
  const me = useSignedInStaff()
  const [scope, setScope] = useState<TeamScope>()
  const [choosing, setChoosing] = useState(false)
  const read = useCaseList(scope ? { facilityId: scope.facilityId, reason: scope.reason } : {})
  const now = useNow()

  useEffect(() => {
    if (scope && read.state.status === 'ready') {
      rememberAccessReasons(
        read.state.data.cases.map((summary) => summary.caseId),
        scope.reason,
      )
    }
  }, [scope, read.state])

  const teamBar = (
    <TeamBar
      scope={scope}
      canChoose={me.role !== 'coordinator'}
      onChoose={() => setChoosing(true)}
      onBack={() => setScope(undefined)}
    />
  )

  return (
    <div className="page">
      {read.state.status === 'ready' ? null : (
        <>
          <Greeting now={now} />
          {teamBar}
        </>
      )}
      <ReadView
        read={read}
        loadingLabel={t('worklist.loading')}
        skeleton="list"
        notAvailable={t('worklist.notAvailable')}
        errorTitle={t('worklist.errorTitle')}
        forbidden={{ title: t('worklist.forbiddenTitle'), body: t('worklist.forbiddenBody') }}
      >
        {(list) => <WorklistBody list={list} now={now} toolbarExtra={teamBar} ownTeam={!scope} />}
      </ReadView>
      <p className="note">{t('worklist.note')}</p>
      {me.facilityId ? (
        <TeamDialog
          open={choosing}
          ownFacility={me.facilityId}
          onChoose={(next) => {
            setChoosing(false)
            setScope(next)
          }}
          onClose={() => setChoosing(false)}
        />
      ) : null}
    </div>
  )
}
