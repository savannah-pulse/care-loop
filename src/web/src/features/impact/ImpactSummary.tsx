import { useState } from 'react'
import { AiSummary } from '../../components'
import { useImpactSummary } from '../../data'
import { useNow } from '../../hooks'
import { t } from '../../i18n'

/**
 * The AI-written reading of the impact figures, shown under the page's heading.
 *
 * @remarks
 * The words come from `GET /ai/impact-summary`, which is written from the same numbers as the
 * cards and tables below, with very small counts withheld. They describe the figures and are not
 * evidence of real-world results; the figures below are the record. "Refresh" asks for a new
 * summary instead of a reused one.
 */
export function ImpactSummary() {
  const now = useNow()
  const [refreshes, setRefreshes] = useState(0)
  return (
    <AiSummary
      read={useImpactSummary(refreshes)}
      now={now}
      label={t('impact.summary.label')}
      loadingLabel={t('impact.summary.loading')}
      refreshing={refreshes > 0}
      onRefresh={() => setRefreshes((count) => count + 1)}
    />
  )
}
