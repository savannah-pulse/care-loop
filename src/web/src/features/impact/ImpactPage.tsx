import { useId, useState, type ReactNode } from 'react'
import { Icon, LoadingState, NotAvailableState, ReadView } from '../../components'
import { useMetrics, useSimulation, type Metrics, type Read, type SimulationArm, type SimulationResult } from '../../data'
import { DISPLAY_LABELS, EXCEPTION_NAMES, displayLabelText, exceptionLabel, isDisplayLabel } from '../../domain'
import {
  facilityName,
  formatCount,
  formatDateTime,
  formatDecimal,
  formatSpan,
  measuredDuration,
  parseInstant,
  toCount,
} from '../../format'
import { useDocumentTitle } from '../../hooks'
import { useSignedInStaff } from '../../identity'
import { hasMessage, t, tp } from '../../i18n'
import {
  MEASURE_KEYS,
  STEP_KEYS,
  barItems,
  confidenceView,
  durationView,
  rateView,
  type BarItem,
  type MeasureKey,
} from './impact'

const CLOSURE_REASONS = [
  'Treated',
  'OnSurveillance',
  'NegativeOnConfirmation',
  'Declined',
  'TransferredAndAccepted',
  'Untraceable',
] as const

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

function measureOf(key: MeasureKey, numerator: number, denominator: number): string {
  return tp(`impact.measureOf.${key}`, denominator, { numerator })
}

function RateRows({ metrics, keys }: { readonly metrics: Metrics; readonly keys: readonly MeasureKey[] }) {
  return (
    <>
      {keys.map((key) => {
        const view = rateView(metrics.measures[key])
        return (
          <tr key={key}>
            <th scope="row">
              {t(`impact.measures.${key}`)}
              <span className="l2">{measureOf(key, view.numerator, view.denominator)}</span>
            </th>
            <td className="num">{view.rate ? <b>{view.rate}</b> : <span className="muted">{t('impact.notMeasured')}</span>}</td>
          </tr>
        )
      })}
    </>
  )
}

function MetricsView({ metrics }: { readonly metrics: Metrics }) {
  const total = toCount(metrics.totalCases)
  const open = toCount(metrics.openCases)
  const closed = toCount(metrics.closedCases)
  const onTime = rateView(metrics.measures.nextStepOnTime)
  const understood = rateView(metrics.measures.understanding)
  const acknowledged = rateView(metrics.measures.acknowledgement)
  const closure = rateView(metrics.measures.closure)
  const confidence = confidenceView(metrics)
  const reached = barItems(metrics.casesReachingDisplayLabel, DISPLAY_LABELS, (key) =>
    isDisplayLabel(key) ? displayLabelText(key) : key,
  )
  const now = barItems(
    metrics.casesByDisplayLabel,
    DISPLAY_LABELS.filter((label) => label !== 'Closed'),
    (key) => (isDisplayLabel(key) ? displayLabelText(key) : key),
  ).filter((item) => item.key !== 'Closed')
  const waiting = barItems(metrics.casesByException, EXCEPTION_NAMES, exceptionLabel)
  const closures = barItems(metrics.closuresByReason, CLOSURE_REASONS, (key) => {
    const message = `closureReasons.${key}`
    return hasMessage(message) ? t(message) : key
  })
  const waitingMax = Math.max(0, ...waiting.map((item) => item.value))
  const closuresTotal = closures.reduce((sum, item) => sum + item.value, 0)
  const nowTotal = now.reduce((sum, item) => sum + item.value, 0)
  return (
    <>
      <div className="kpis">
        <Kpi
          label={t('impact.measures.nextStepOnTime')}
          value={onTime.rate ?? t('impact.notMeasured')}
          of={measureOf('nextStepOnTime', onTime.numerator, onTime.denominator)}
          detail={
            closure.rate
              ? t('impact.closedDetail', { rate: closure.rate, closed, total })
              : t('impact.closedNone', { total })
          }
        />
        <Kpi
          label={t('impact.overdueLabel')}
          value={formatCount(toCount(metrics.overdueCases))}
          of={tp('impact.overdueOf', open)}
          detail={t('impact.overdueDetail', {
            escalated: toCount(metrics.escalatedCases),
            unassigned: toCount(metrics.unassignedCases),
          })}
        />
        <Kpi
          label={t('impact.measures.understanding')}
          value={understood.rate ?? t('impact.notMeasured')}
          of={measureOf('understanding', understood.numerator, understood.denominator)}
          detail={
            acknowledged.rate
              ? t('impact.ackDetail', {
                  rate: acknowledged.rate,
                  numerator: acknowledged.numerator,
                  denominator: acknowledged.denominator,
                })
              : t('impact.ackNone')
          }
        />
      </div>
      <div className="grid-even">
        <Card title={t('impact.stepsTitle')} aside={t('impact.stepsAside')}>
          <table className="mini">
            <thead>
              <tr>
                <th scope="col">{t('impact.columns.step')}</th>
                <th scope="col" className="num">
                  {t('impact.columns.median')}
                </th>
                <th scope="col" className="num">
                  {t('impact.columns.cases')}
                </th>
              </tr>
            </thead>
            <tbody>
              {STEP_KEYS.map((key) => {
                const view = durationView(metrics.timeToStep[key])
                return (
                  <tr key={key}>
                    <th scope="row">{t(`impact.steps.${key}`)}</th>
                    <td className="num">
                      {view.median ? <b>{view.median}</b> : <span className="muted">{t('impact.notReached')}</span>}
                    </td>
                    <td className="num">{formatCount(view.cases)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Card>
        <Card title={t('impact.completedTitle')} aside={t('impact.completedAside')}>
          <table className="mini">
            <thead>
              <tr>
                <th scope="col">{t('impact.columns.measure')}</th>
                <th scope="col" className="num">
                  {t('impact.columns.rate')}
                </th>
              </tr>
            </thead>
            <tbody>
              <RateRows
                metrics={metrics}
                keys={MEASURE_KEYS.filter((key) => key !== 'nextStepOnTime' && key !== 'understanding')}
              />
              <tr>
                <th scope="row">
                  {t('impact.confidence')}
                  <span className="l2">
                    {tp('impact.confidenceOf', confidence.responses, {
                      told: confidence.told,
                      rate: confidence.rate ?? t('impact.notMeasured'),
                    })}
                  </span>
                </th>
                <td className="num">
                  {confidence.average === undefined ? (
                    <span className="muted">{t('impact.notMeasured')}</span>
                  ) : (
                    <b>{formatDecimal(confidence.average)}</b>
                  )}
                </td>
              </tr>
            </tbody>
          </table>
        </Card>
      </div>
      <div className="grid-even">
        <Card title={t('impact.reachedTitle')} aside={t('impact.outOf', { count: total })}>
          <Bars items={reached} scale={total} />
        </Card>
        <Card title={t('impact.nowTitle')} aside={t('impact.outOf', { count: open })}>
          {nowTotal === 0 ? <p className="note">{t('impact.nowNone')}</p> : <Bars items={now} scale={Math.max(open, nowTotal)} />}
        </Card>
      </div>
      <div className="grid-even">
        <Card title={t('impact.byExceptionTitle')} aside={t('impact.byExceptionOf')}>
          {waitingMax === 0 ? (
            <p className="note">{t('impact.byExceptionNone')}</p>
          ) : (
            <Bars items={waiting} scale={waitingMax} amber />
          )}
        </Card>
        <Card title={t('impact.closuresTitle')} aside={t('impact.outOf', { count: closed })}>
          {closuresTotal === 0 ? (
            <p className="note">{t('impact.closuresNone')}</p>
          ) : (
            <>
              <Bars items={closures} scale={Math.max(closed, closuresTotal)} />
              <p className="note linkrow">{t('impact.closuresNote')}</p>
            </>
          )}
        </Card>
      </div>
    </>
  )
}

function medianText(arm: SimulationArm): string {
  const median = measuredDuration(arm.medianTimeToClosure)
  return median === undefined ? t('impact.rows.noneClosed') : formatSpan(median)
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

function ComparisonBody({ read, labelledBy }: { readonly read: Read<SimulationResult>; readonly labelledBy: string }) {
  const { state, retry } = read
  if (state.status === 'loading') {
    return <LoadingState label={t('impact.comparisonLoading')} />
  }
  if (state.status === 'ready') {
    return <Comparison result={state.data} labelledBy={labelledBy} />
  }
  return (
    <NotAvailableState
      title={t('impact.comparisonUnavailableTitle')}
      body={t('impact.comparisonUnavailableBody')}
      onCheckAgain={retry}
    />
  )
}

/** Props for {@link ComparisonCard}. */
export interface ComparisonCardProps {
  /** The read of `POST /metrics/simulation`, from `useSimulation`. */
  readonly read: Read<SimulationResult>
}

/**
 * The card "What reminders and escalation change": a cohort replayed with reminders and escalation
 * on and off, side by side.
 *
 * @remarks
 * Left out entirely until the API answers with the comparison, and for as long as it answers that
 * the comparison is not built yet (HTTP 501, WS10), so the page never shows a placeholder for it.
 * Any other failure shows the card with a calm "isn't available right now" and a way to check
 * again, and the card stays in place while the comparison runs again. The comparison is never
 * repeated by itself, because each run replays a whole cohort.
 */
export function ComparisonCard({ read }: ComparisonCardProps) {
  const headingId = useId()
  const [shown, setShown] = useState(false)
  const { state } = read
  const answered = state.status === 'ready' || (state.status === 'failed' && state.failure.kind !== 'notAvailable')
  if (answered && !shown) {
    setShown(true)
  }
  if (!answered && !(shown && state.status === 'loading')) {
    return null
  }
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('impact.comparisonTitle')}
      </h2>
      <ComparisonBody read={read} labelledBy={headingId} />
    </section>
  )
}

/**
 * Screen 3, impact: whether actionable results lead to timely care and closed loops, each measure
 * with what it counts and what it is out of, and what reminders and escalation change.
 *
 * @remarks
 * Read-only. The cards and tables come from `GET /metrics`, read again quietly every 30 seconds
 * while the page is visible: the facility of the signed-in person, or every facility for the
 * programme coordinator. Rates show their numerator and denominator, and
 * an empty population says "not measured yet" instead of 0%. Times to each step are medians of
 * clock time from the result. The comparison comes from `POST /metrics/simulation`, which replays a
 * cohort with reminders and escalation on and off; while it is not built (501) the card is left out,
 * and any other failure says it is not available right now. These numbers show how CareLoop works;
 * they are not evidence of real-world results.
 */
export function ImpactPage() {
  useDocumentTitle(t('impact.title'))
  const me = useSignedInStaff()
  const metrics = useMetrics()
  const simulation = useSimulation()
  const data = metrics.state.status === 'ready' ? metrics.state.data : undefined
  const generatedAt = data ? parseInstant(data.generatedAt) : undefined
  const scope = me.facilityId ? facilityName(me.facilityId) : t('impact.allFacilities')
  return (
    <div className="page">
      <div className="page-head">
        <h1>{t('impact.title')}</h1>
        {data && generatedAt ? (
          <p className="lede">
            {t('impact.subtitle', {
              scope,
              date: formatDateTime(generatedAt),
              count: tp('impact.cases', toCount(data.totalCases)),
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
        {(loaded) => <MetricsView metrics={loaded} />}
      </ReadView>
      <ComparisonCard read={simulation} />
      <p className="note">{t('impact.footnote')}</p>
    </div>
  )
}
