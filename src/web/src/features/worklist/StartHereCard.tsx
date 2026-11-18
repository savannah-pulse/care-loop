import { useId } from 'react'
import { AiWords, Badge, Icon, LoadingState } from '../../components'
import { isAiSwitchedOff, useRoleBriefing } from '../../data'
import { caseNumber, nextActionText } from '../../domain'
import { describeDue, formatWhen } from '../../format'
import { useSignedInStaff } from '../../identity'
import { t } from '../../i18n'
import { routeHref } from '../../routing'
import { START_REASON_TONES, startHere, type StartHereItem } from './startHere'
import { isMine, nextDeadline, type WorklistRow } from './worklist'

/** Props for {@link StartHereCard}. */
export interface StartHereCardProps {
  /** The open cases of the person's team, before any filter, search or scope. */
  readonly rows: readonly WorklistRow[]
  /** The current time. */
  readonly now: Date
}

function StartRow({ item, position, now }: { readonly item: StartHereItem; readonly position: number; readonly now: Date }) {
  const { row, reason } = item
  const { summary, patient } = row
  const due = row.due ? describeDue(row.due, now) : undefined
  const reasonText = t(`worklist.start.reasons.${reason}`)
  return (
    <li className="start-row">
      <span className="start-n" aria-hidden="true">
        {position}
      </span>
      <span className="start-who">
        <a
          className="name"
          href={routeHref({ name: 'case', caseId: summary.caseId })}
          title={patient.reference}
          aria-label={t('worklist.start.linkName', { name: row.linkName, reason: reasonText })}
        >
          {patient.name}
        </a>
        {patient.hidden ? null : (
          <span className="l2">{t('patient.case', { number: caseNumber(summary.caseId) })}</span>
        )}
      </span>
      <span className="start-what">
        <span className="action">{nextActionText(summary.nextAction)}</span>
        <span className="l2">
          {due ? [due.primary, due.secondary].join(t('common.separator')) : t('time.noDueTime')}
        </span>
      </span>
      <span className="start-status">
        <Badge tone={START_REASON_TONES[reason]}>{reasonText}</Badge>
      </span>
    </li>
  )
}

/**
 * The one optional sentence the role-briefing agent writes about the person's worklist.
 *
 * @remarks
 * Asks `POST /ai/role-briefing` when it mounts. While AI is switched off (409) it shows nothing; while
 * it loads, one skeleton line; when the agent could not write it (503, or any other failure), a small
 * calm note. The cases above never depend on it. Under the words, in small muted text, it names the
 * agent and prompt versions that wrote them.
 */
function BriefingLine() {
  const { state } = useRoleBriefing()
  if (state.status === 'loading') {
    return (
      <div className="start-ai">
        <LoadingState label={t('worklist.start.aiLoading')} shape="line" />
      </div>
    )
  }
  if (state.status === 'ready') {
    return (
      <div className="start-ai">
        <AiWords text={state.data.text} caption={t('worklist.start.aiCaption')} chip={t('ai.chip')} versions={state.data} />
      </div>
    )
  }
  if (isAiSwitchedOff(state.failure.kind) || state.failure.kind === 'aborted') {
    return null
  }
  return (
    <div className="start-ai">
      <p className="ai-note" role="status">
        <Icon name="alert" />
        <span>{t('worklist.start.aiFailed')}</span>
      </p>
    </div>
  )
}

/**
 * "Start here today": a small card above the worklist's groups listing up to three of the signed-in
 * person's cases, overdue or escalated first, then urgent, then due today, each linking to its case.
 *
 * @remarks
 * CareLoop works the list out from the worklist it already loaded (see `startHere`), so the card
 * works without AI. When it lists cases, one sentence from the role-briefing agent may sum them up,
 * captioned as written by AI from the worklist; AI is never asked when there is nothing to sum up.
 * When none of the person's cases is overdue, escalated, urgent or due today, it says so in one calm
 * line, with their next deadline when they have one. Names follow the role: the programme
 * coordinator sees case numbers only.
 */
export function StartHereCard({ rows, now }: StartHereCardProps) {
  const me = useSignedInStaff()
  const headingId = useId()
  const items = startHere(rows, me, now)
  const mineNext = items.length === 0 ? nextDeadline(rows.filter((row) => isMine(row, me))) : undefined
  return (
    <section className="card start-here" aria-labelledby={headingId}>
      <div className="start-head">
        <h2 className="card-title" id={headingId}>
          {t('worklist.start.title')}
        </h2>
        <p className="start-source">
          <Icon name="clock" />
          <span>{t('worklist.start.source')}</span>
        </p>
      </div>
      {items.length === 0 ? (
        <p className="start-none">
          {mineNext
            ? t('worklist.start.noneNext', { date: formatWhen(mineNext, now) })
            : t('worklist.start.none')}
        </p>
      ) : (
        <>
          <p className="start-intro">{t('worklist.start.intro')}</p>
          <ol className="start-list">
            {items.map((item, index) => (
              <StartRow key={item.row.summary.caseId} item={item} position={index + 1} now={now} />
            ))}
          </ol>
          <BriefingLine />
        </>
      )}
    </section>
  )
}
