import { useState } from 'react'
import { AiSummary } from '../../components'
import { useImpactSummary } from '../../data'
import { useNow } from '../../hooks'
import { t } from '../../i18n'

/** Props for {@link ImpactSummary}. */
export interface ImpactSummaryProps {
  /** The figures the summary describes, as text; when it changes the summary is read again. */
  readonly snapshot?: string
}

/**
 * The AI-written reading of the impact figures, shown under the page's heading.
 *
 * @remarks
 * The words come from `GET /ai/impact-summary`, which is written from the same numbers as the
 * cards and tables below, with very small counts withheld. They describe the figures and are not
 * evidence of real-world results; the figures below are the record. "Refresh" asks for a new
 * summary instead of a reused one.
 */
export function ImpactSummary({ snapshot }: ImpactSummaryProps) {
  const now = useNow()
  const [refreshes, setRefreshes] = useState(0)
  return (
    <AiSummary
      read={useImpactSummary(refreshes, snapshot)}
      now={now}
      label={t('impact.summary.label')}
      loadingLabel={t('impact.summary.loading')}
      refreshing={refreshes > 0}
      onRefresh={() => setRefreshes((count) => count + 1)}
    />
  )
}
