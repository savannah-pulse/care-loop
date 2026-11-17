import { useId } from 'react'
import { AiChip, AiWords, Icon, LoadingState } from '../../components'
import type { AiText, Read } from '../../data'
import { t } from '../../i18n'

/** Props for {@link NumbersSayCard}. */
export interface NumbersSayCardProps {
  /** The read of `POST /ai/impact-summary`, started beside the read of the numbers. */
  readonly read: Read<AiText>
}

function Body({ read }: NumbersSayCardProps) {
  const { state } = read
  if (state.status === 'loading') {
    return <LoadingState label={t('impact.ai.loading')} />
  }
  if (state.status === 'ready') {
    return <AiWords text={state.data.text} caption={t('impact.ai.caption')} className="numbers-say-words" />
  }
  if (state.failure.kind === 'notAvailable') {
    return (
      <div className="ai-quiet" role="status">
        <p>{t('impact.ai.notReady')}</p>
        <p className="note">{t('impact.ai.notReadyNote')}</p>
      </div>
    )
  }
  return (
    <p className="ai-note" role="status">
      <Icon name="alert" />
      <span>{t('impact.ai.failed')}</span>
    </p>
  )
}

/**
 * "What the numbers say": a few plain sentences the impact-summary agent wrote from the measures on
 * the Impact screen, captioned as written by AI from those numbers, to be checked before anyone acts
 * on them.
 *
 * @remarks
 * CareLoop works out every number on the page; the agent only puts them into words, from the totals,
 * never from a case or a name. The card loads on its own, so the numbers never wait for it. While it
 * loads it shows a skeleton; while AI is switched off (501) one calm line that it is not ready yet;
 * when the agent could not write it (503, or any other failure) a calm note that every number is
 * still up to date. No half-written text is ever shown.
 */
export function NumbersSayCard({ read }: NumbersSayCardProps) {
  const headingId = useId()
  return (
    <section className="card numbers-say" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('impact.ai.title')}
        {read.state.status === 'ready' ? (
          <span className="r">
            <AiChip label={t('ai.draftChip')} />
          </span>
        ) : null}
      </h2>
      <Body read={read} />
    </section>
  )
}
