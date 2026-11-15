import { useCallback, useState } from 'react'
import type { CareLoopApi } from '../../api/client'
import { useResource } from '../../api/useResource'
import { ResourceStatus } from '../../components/ResourceStatus'
import { codeText, t } from '../../i18n'
import { dateText, durationText, numeric, rateText } from '../../format'

/** Props for the coordination impact screen. */
export interface ImpactProps {
  /** The current-identity API client. */
  api: CareLoopApi
  /** Identity boundary for aggregate reads. */
  identityKey: string
}

/** Presents event-derived rates and elapsed medians with explicit denominators; no AI narrative or invented comparison. */
export function Impact({ api, identityKey }: ImpactProps) {
  const [refresh, setRefresh] = useState(0)
  const load = useCallback((signal: AbortSignal) => api.metrics(signal), [api])
  const resource = useResource(`${identityKey}/metrics/${refresh}`, load)
  const metrics = resource.data
  return <section><div className="page-heading"><div><h1>{t('impact.title')}</h1><p>{t('impact.description')}</p></div>
    <button onClick={() => setRefresh((n) => n + 1)}>{t('common.refresh')}</button></div>
    <ResourceStatus loading={resource.loading} error={resource.error} />
    {metrics && <>
      <p className="muted">{dateText(metrics.generatedAt)}</p>
      <div className="grid four">{([
        ['impact.total', metrics.totalCases], ['impact.open', metrics.openCases],
        ['impact.overdue', metrics.overdueCases], ['impact.closed', metrics.closedCases],
      ] as const).map(([key, value]) => <div className="card stat" key={key}><small>{t(key)}</small><strong>{value}</strong></div>)}</div>
      <div className="grid three">{Object.entries(metrics.measures).map(([key, measure]) => <article className="card stat" key={key}>
        <small>{codeText('measure', key)}</small><strong>{rateText(measure.rate)}</strong>
        <p>{t('common.count', { numerator: measure.numerator, denominator: measure.denominator })}</p></article>)}</div>
      <div className="grid two"><div className="card"><h2>{t('impact.steps')}</h2><p className="muted">{t('common.clockTime')}</p>
        {Object.entries(metrics.timeToStep).map(([key, step]) => <div className="detail-row" key={key}>
          <span>{codeText('step', key)}<small>{step.denominator}</small></span><strong>{durationText(step.median)}</strong></div>)}</div>
        <div className="card"><h2>{t('impact.reached')}</h2>
          {Object.entries(metrics.casesReachingDisplayLabel).map(([key, measure]) => <div className="progress-row" key={key}>
            <span>{codeText('label', key)}</span><progress aria-label={codeText('label', key)}
              max={Math.max(1, numeric(measure.denominator))} value={numeric(measure.numerator)} />
            <small>{t('common.count', { numerator: measure.numerator, denominator: measure.denominator })}</small></div>)}</div></div>
      <div className="grid three"><div className="card"><h2>{t('impact.exceptions')}</h2>{Object.entries(metrics.casesByException).map(([key, count]) =>
        <div className="detail-row" key={key}><span>{codeText('exception', key)}</span><strong>{count}</strong></div>)}</div>
        <div className="card"><h2>{t('impact.closures')}</h2>{Object.entries(metrics.closuresByReason).map(([key, count]) =>
          <div className="detail-row" key={key}><span>{codeText('closure', key)}</span><strong>{count}</strong></div>)}</div>
        <div className="card stat"><small>{t('impact.confidence')}</small><strong>{metrics.averageConfidence == null
          ? t('common.notMeasured') : new Intl.NumberFormat('en', { maximumFractionDigits: 2 }).format(numeric(metrics.averageConfidence))}</strong>
          <p>{t('impact.confidenceParticipation')}: {rateText(metrics.confidenceRate)}</p>
          <p>{t('common.count', { numerator: metrics.confidenceResponses, denominator: metrics.confidenceDenominator })}</p></div></div>
    </>}
    <p className="notice">{t('impact.comparison')}</p>
  </section>
}
