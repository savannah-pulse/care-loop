import { useState } from 'react'
import { Icon } from '../../components'
import { useRoleBriefing } from '../../data'
import { formatWhen } from '../../format'
import { t } from '../../i18n'

/** Props for {@link RoleBriefing}. */
export interface RoleBriefingProps {
  /** The current time, to say when the summary was written. */
  readonly now: Date
}

/**
 * The AI-written summary of the signed-in person's open cases, shown under the worklist's greeting.
 *
 * @remarks
 * The words come from `GET /ai/role-briefing` and are shown as plain text, labelled as written by
 * AI, with when they were written. They are a reading aid: the worklist below is the record. When
 * AI is switched off, or could not write the summary, nothing is shown, because nothing else on the
 * page depends on it. "Refresh" asks for a new summary instead of a reused one.
 */
export function RoleBriefing({ now }: RoleBriefingProps) {
  const [refreshes, setRefreshes] = useState(0)
  const { state } = useRoleBriefing(refreshes)

  if (state.status === 'failed') {
    return null
  }
  if (state.status === 'loading') {
    return (
      <p className="ai-brief-meta muted" role="status">
        {t(refreshes > 0 ? 'worklist.briefing.refreshing' : 'worklist.briefing.loading')}
      </p>
    )
  }
  return (
    <section className="ai-brief" aria-label={t('worklist.briefing.label')}>
      <p className="ai-brief-text">{state.data.text}</p>
      <p className="ai-brief-meta muted">
        <Icon name="spark" />
        <span>
          {t('worklist.briefing.writtenByAi')}
          {t('common.separator')}
          {formatWhen(new Date(state.data.generatedAt), now)}
        </span>
        <button type="button" className="ai-brief-refresh" onClick={() => setRefreshes((count) => count + 1)}>
          {t('worklist.briefing.refresh')}
        </button>
      </p>
    </section>
  )
}
