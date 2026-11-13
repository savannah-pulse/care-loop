import { useId, type ReactNode } from 'react'
import { Icon, ReadView } from '../../components'
import { useMetrics, useSimulation, type Metrics, type SimulationArm, type SimulationResult } from '../../data'
import { CASE_STAGES, EXCEPTION_NAMES, exceptionLabel, stageLabel } from '../../domain'
import {
  formatCount,
  formatDateTime,
  formatDuration,
  formatPercent,
  parseInstant,
  parseTimeSpan,
  toCount,
  toNumber,
} from '../../format'
import { useDocumentTitle } from '../../hooks'
import { t, tp } from '../../i18n'

interface BarItem {
  readonly key: string
  readonly label: string
  readonly value: number
}

interface BarsProps {
  readonly items: readonly BarItem[]
  readonly scale: number
  readonly amber?: boolean
}

function Bars({ items, scale, amber = false }: BarsProps) {
  return (
    <ul className="bars">
      {items.map((item) => (
        <li key={item.key}>
          <span>{item.label}</span>
          <span className={amber ? 'bar amberbar' : 'bar'} aria-hidden="true">
            <i style={{ inlineSize: `${scale > 0 ? Math.round((item.value / scale) * 1000) / 10 : 0}%` }} />
          </span>
          <span className="v">{formatCount(item.value)}</span>
        </li>
      ))}
    </ul>
  )
}

function countsIn(
  values: Readonly<Record<string, number | string>>,
  known: readonly string[],
  label: (key: string) => string,
): BarItem[] {
  const keys = [...known, ...Object.keys(values).filter((key) => !known.includes(key))]
  return keys.map((key) => ({ key, label: label(key), value: toCount(values[key]) }))
}

interface KpiProps {
  readonly label: string
  readonly value: string
  readonly of: string
  readonly detail: string
}

function Kpi({ label, value, of, detail }: KpiProps) {
  const headingId = useId()
  return (
    <section className="card kpi" aria-labelledby={headingId}>
      <h2 className="lbl" id={headingId}>
        {label}
      </h2>
      <p className="big">{value}</p>
      <p className="of">{of}</p>
      <p className="vs">{detail}</p>
    </section>
  )
}

interface CardProps {
  readonly title: string
  readonly aside?: string
  readonly children: ReactNode
}

function Card({ title, aside, children }: CardProps) {
  const headingId = useId()
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {title}
        {aside ? <span className="r small muted">{aside}</span> : null}
      </h2>
      {children}
    </section>
  )
}

function MetricsView({ metrics }: { readonly metrics: Metrics }) {
  const total = toCount(metrics.totalCases)
  const open = toCount(metrics.openCases)
  const closed = toCount(metrics.closedCases)
  const closureRate = toNumber(metrics.closureRate)
  const acknowledgementRate = toNumber(metrics.acknowledgementRate)
  const median = parseTimeSpan(metrics.medianTimeToClosure)
  const byStage = countsIn(
    metrics.casesByStage,
    CASE_STAGES.filter((stage) => stage !== 'Closed'),
    (key) => ((CASE_STAGES as readonly string[]).includes(key) ? stageLabel(key as (typeof CASE_STAGES)[number]) : key),
  )
  const byException = countsIn(metrics.casesByException, EXCEPTION_NAMES, exceptionLabel)
  const stageTotal = byStage.reduce((sum, item) => sum + item.value, 0)
  const exceptionMax = Math.max(0, ...byException.map((item) => item.value))
  return (
    <>
      <div className="kpis">
        <Kpi
          label={t('impact.closedLabel')}
          value={closureRate === undefined ? t('impact.noRate') : formatPercent(closureRate)}
          of={t('impact.closedOf', { closed, total })}
          detail={
            median === undefined
              ? t('impact.noClosureYet')
              : t('impact.medianClosure', { duration: formatDuration(median, 'fine') })
          }
        />
        <Kpi
          label={t('impact.overdueLabel')}
          value={formatCount(toCount(metrics.overdueCases))}
          of={t('impact.overdueOf', { open })}
          detail={t('impact.overdueDetail', {
            escalated: toCount(metrics.escalatedCases),
            unassigned: toCount(metrics.unassignedCases),
          })}
        />
        <Kpi
          label={t('impact.ackLabel')}
          value={acknowledgementRate === undefined ? t('impact.noRate') : formatPercent(acknowledgementRate)}
          of={t('impact.ackOf')}
          detail={t('impact.ackDetail')}
        />
      </div>
      <div className="grid-even">
        <Card title={t('impact.byStageTitle')} aside={t('impact.byStageOf', { open })}>
          <Bars items={byStage} scale={Math.max(open, stageTotal)} />
        </Card>
        <Card title={t('impact.byExceptionTitle')} aside={t('impact.byExceptionOf')}>
          {exceptionMax === 0 ? (
            <p className="note">{t('impact.byExceptionNone')}</p>
          ) : (
            <Bars items={byException} scale={exceptionMax} amber />
          )}
        </Card>
      </div>
    </>
  )
}

function medianText(arm: SimulationArm): string {
  const median = parseTimeSpan(arm.medianTimeToClosure)
  return median === undefined ? t('impact.rows.noneClosed') : formatDuration(median, 'fine')
}

function closedText(arm: SimulationArm): string {
  return t('impact.rows.closedValue', { closed: toCount(arm.closedCases), total: toCount(arm.totalCases) })
}

interface ComparisonRowProps {
  readonly label: string
  readonly detail?: string
  readonly withEscalation: string
  readonly escalationOff: string
}

function ComparisonRow({ label, detail, withEscalation, escalationOff }: ComparisonRowProps) {
  return (
    <tr>
      <th scope="row">
        {label}
        {detail ? <span className="l2">{detail}</span> : null}
      </th>
      <td className="num">
        <b>{withEscalation}</b>
      </td>
      <td className="num">{escalationOff}</td>
    </tr>
  )
}

function Comparison({ result, labelledBy }: { readonly result: SimulationResult; readonly labelledBy: string }) {
  const on = result.withEscalation
  const off = result.withoutEscalation
  return (
    <>
      <table className="mini cmp" aria-labelledby={labelledBy}>
        <thead>
          <tr>
            <th scope="col">{t('impact.measure')}</th>
            <th scope="col" className="num">
              {t('impact.withEscalation')}
            </th>
            <th scope="col" className="num">
              {t('impact.escalationOff')}
            </th>
          </tr>
        </thead>
        <tbody>
          <ComparisonRow
            label={t('impact.rows.closed')}
            detail={t('impact.rows.closedDetail')}
            withEscalation={closedText(on)}
            escalationOff={closedText(off)}
          />
          <ComparisonRow
            label={t('impact.rows.stalled')}
            detail={t('impact.rows.stalledDetail')}
            withEscalation={formatCount(toCount(on.stalledCases))}
            escalationOff={formatCount(toCount(off.stalledCases))}
          />
          <ComparisonRow
            label={t('impact.rows.escalations')}
            withEscalation={formatCount(toCount(on.escalationsRaised))}
            escalationOff={formatCount(toCount(off.escalationsRaised))}
          />
          <ComparisonRow
            label={t('impact.rows.median')}
            detail={t('impact.rows.medianDetail')}
            withEscalation={medianText(on)}
            escalationOff={medianText(off)}
          />
        </tbody>
      </table>
      <details className="disclosure method">
        <summary>
          {t('impact.methodTitle')}
          <Icon name="down" />
        </summary>
        <p>{t('impact.methodBody')}</p>
      </details>
    </>
  )
}

/**
 * Screen 3, impact: whether actionable results lead to closed loops, and what escalation changes.
 *
 * @remarks
 * Read-only. The headline cards and bars come from `GET /metrics`. The comparison comes from
 * `POST /metrics/simulation`, which replays a cohort scenario twice, with reminders and escalation
 * on and off; it illustrates the coordination effect and is not evidence of real-world results.
 * The scenario's name and seed stay out of view; the method is explained under "How this is worked
 * out". Only measures the API returns are shown; the fuller set in the design waits for C5.
 */
export function ImpactPage() {
  useDocumentTitle(t('impact.title'))
  const metrics = useMetrics()
  const simulation = useSimulation()
  const comparisonId = useId()
  const generatedAt = metrics.state.status === 'ready' ? parseInstant(metrics.state.data.generatedAt) : undefined
  return (
    <div className="page">
      <div className="page-head">
        <h1>{t('impact.title')}</h1>
        {metrics.state.status === 'ready' && generatedAt ? (
          <p className="lede">
            {t('impact.subtitle', {
              date: formatDateTime(generatedAt),
              count: tp('impact.cases', toCount(metrics.state.data.totalCases)),
            })}
          </p>
        ) : null}
      </div>
      <ReadView
        read={metrics}
        loadingLabel={t('impact.loading')}
        skeleton="cards"
        notAvailable={t('impact.notAvailable')}
        errorTitle={t('impact.errorTitle')}
      >
        {(data) => <MetricsView metrics={data} />}
      </ReadView>
      <section className="card" aria-labelledby={comparisonId}>
        <h2 className="card-title" id={comparisonId}>
          {t('impact.comparisonTitle')}
        </h2>
        <ReadView
          read={simulation}
          loadingLabel={t('impact.comparisonLoading')}
          notAvailable={t('impact.comparisonNotAvailable')}
          errorTitle={t('impact.comparisonError')}
        >
          {(result) => <Comparison result={result} labelledBy={comparisonId} />}
        </ReadView>
      </section>
    </div>
  )
}
