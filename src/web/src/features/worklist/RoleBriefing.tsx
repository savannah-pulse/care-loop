import { useState } from 'react'
import { AiSummary } from '../../components'
import { useRoleBriefing } from '../../data'
import { t } from '../../i18n'

/** Props for {@link RoleBriefing}. */
export interface RoleBriefingProps {
  /** The current time, to say when the summary was written. */
  readonly now: Date
  /** The worklist the summary describes, as text; when it changes the summary is read again. */
  readonly snapshot?: string
}

/**
 * The AI-written summary of the signed-in person's open cases, shown under the worklist's greeting.
 *
 * @remarks
 * The words come from `GET /ai/role-briefing`. They are a reading aid: the worklist below is the
 * record. "Refresh" asks for a new summary instead of a reused one. The summary is read again when the
 * worklist changes, so it does not stay beside a newer list.
 */
export function RoleBriefing({ now, snapshot }: RoleBriefingProps) {
  const [refreshes, setRefreshes] = useState(0)
  return (
    <AiSummary
      read={useRoleBriefing(refreshes, snapshot)}
      now={now}
      label={t('worklist.briefing.label')}
      loadingLabel={t('worklist.briefing.loading')}
      refreshing={refreshes > 0}
      onRefresh={() => setRefreshes((count) => count + 1)}
    />
  )
}
