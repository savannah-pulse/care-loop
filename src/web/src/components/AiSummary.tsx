import { useId, useLayoutEffect, useRef, useState } from 'react'
import type { AiText, Read } from '../data'
import { formatWhen } from '../format'
import { t } from '../i18n'
import { Icon } from './Icon'

/** Props for {@link AiSummary}. */
export interface AiSummaryProps {
  /** The read of the AI text, from `GET /ai/role-briefing` or `GET /ai/impact-summary`. */
  readonly read: Read<AiText>
  /** The current time, to say when the text was written. */
  readonly now: Date
  /** The accessible name of the summary, for example "Summary written by AI". */
  readonly label: string
  /** What to say while the first text is being written. */
  readonly loadingLabel: string
  /** `true` once the reader has asked for a new text, so the wait says so. */
  readonly refreshing: boolean
  /** Called when the reader asks for a new text instead of a reused one. */
  readonly onRefresh: () => void
}

/**
 * A short text written by AI, shown as plain text with who wrote it, when, and a way to ask again.
 *
 * @remarks
 * The words are a reading aid: the figures or the list beside them are the record. They are never
 * rendered as markup. When AI is switched off, or could not write the text, nothing is shown,
 * because nothing else on the page depends on it. Paragraphs in the text are kept. A text longer
 * than three lines is cut there with a "Show more" button, so it does not push the record it
 * describes off the screen; a shorter one is shown whole, with no button.
 */
export function AiSummary({ read, now, label, loadingLabel, refreshing, onRefresh }: AiSummaryProps) {
  const { state } = read
  const text = state.status === 'ready' ? state.data.text : ''
  const textId = useId()
  const textRef = useRef<HTMLParagraphElement>(null)
  const [expanded, setExpanded] = useState(false)
  const [overflows, setOverflows] = useState(false)
  // Whether the cut text hides anything depends on its width, so measure again when the window changes.
  useLayoutEffect(() => {
    const measure = () => {
      const element = textRef.current
      if (element && !expanded) {
        setOverflows(element.scrollHeight > element.clientHeight + 1)
      }
    }
    measure()
    window.addEventListener('resize', measure)
    return () => window.removeEventListener('resize', measure)
  }, [text, expanded])
  if (state.status === 'failed') {
    return null
  }
  if (state.status === 'loading') {
    return (
      <p className="ai-brief-meta muted" role="status">
        {refreshing ? t('ai.refreshing') : loadingLabel}
      </p>
    )
  }
  return (
    <section className="ai-brief" aria-label={label}>
      <p id={textId} ref={textRef} className={expanded ? 'ai-brief-text' : 'ai-brief-text ai-brief-clamped'}>
        {state.data.text}
      </p>
      <p className="ai-brief-meta muted">
        <Icon name="spark" />
        <span>
          {t('ai.writtenByAi')}
          {t('common.separator')}
          {formatWhen(new Date(state.data.generatedAt), now)}
        </span>
        <button type="button" className="ai-brief-refresh" onClick={onRefresh}>
          {t('ai.refresh')}
        </button>
        {(overflows || expanded) && (
          <button
            type="button"
            className="ai-brief-refresh"
            aria-expanded={expanded}
            aria-controls={textId}
            onClick={() => setExpanded((open) => !open)}
          >
            {expanded ? t('ai.showLess') : t('ai.showMore')}
          </button>
        )}
      </p>
    </section>
  )
}
