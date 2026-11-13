import type { ReactNode } from 'react'
import type { Read } from '../data'
import { formatTime } from '../format'
import { t } from '../i18n'
import { Icon, type IconName } from './Icon'

/**
 * The shapes a loading placeholder can take, each roughly the shape of what will appear.
 *
 * @remarks
 * - `lines`: a few lines of text, as in a timeline.
 * - `list`: a short list of cases, as on the worklist.
 * - `cards`: a row of number cards, as on the impact screen.
 * - `page`: a heading block over a card, as on a case.
 */
export type SkeletonShape = 'lines' | 'list' | 'cards' | 'page'

/** Props for {@link LoadingState}. */
export interface LoadingStateProps {
  /** What is loading, read out by screen readers only, for example `Loading your worklist`. */
  readonly label: string
  /** The shape of the placeholder; `lines` by default. */
  readonly shape?: SkeletonShape
}

function Bar({ size }: { readonly size: string }) {
  return <span className="sk" style={{ inlineSize: size }} />
}

function Lines() {
  return (
    <>
      <Bar size="72%" />
      <Bar size="94%" />
      <Bar size="58%" />
    </>
  )
}

function Shape({ shape }: { readonly shape: SkeletonShape }) {
  switch (shape) {
    case 'lines':
      return <Lines />
    case 'list':
      return (
        <>
          {['a', 'b', 'c'].map((key) => (
            <span key={key} className="sk-card">
              <Bar size="38%" />
              <Bar size="80%" />
              <Bar size="55%" />
            </span>
          ))}
        </>
      )
    case 'cards':
      return (
        <span className="sk-row">
          {['a', 'b', 'c'].map((key) => (
            <span key={key} className="sk-card">
              <Bar size="60%" />
              <span className="sk sk-big" />
              <Bar size="85%" />
            </span>
          ))}
        </span>
      )
    case 'page':
      return (
        <>
          <span className="sk-card">
            <Bar size="45%" />
            <Bar size="70%" />
          </span>
          <span className="sk-card">
            <Lines />
          </span>
        </>
      )
  }
}

/**
 * Grey placeholder shapes shown while data loads, instead of a "loading" message.
 *
 * @remarks
 * The placeholder is hidden from assistive technology; screen readers hear the label once, politely.
 * Its gentle shimmer stops for people who prefer reduced motion.
 */
export function LoadingState({ label, shape = 'lines' }: LoadingStateProps) {
  return (
    <div className={`skeleton skeleton-${shape}`} role="status">
      <span className="visually-hidden">{label}</span>
      <span className="skeleton-shape" aria-hidden="true">
        <Shape shape={shape} />
      </span>
    </div>
  )
}

/** Props for {@link NotAvailableState}. */
export interface NotAvailableStateProps {
  /** The heading; `This part isn't ready yet` by default. */
  readonly title?: string
  /** What is not available and what to expect. */
  readonly body: string
  /** The icon in front: a clock for "not yet" by default, a lock for "not for your role". */
  readonly icon?: IconName
  /** Called when the person asks to check again; no button when absent. */
  readonly onCheckAgain?: () => void
}

/**
 * A calm notice that a part of CareLoop is not ready yet, or not for the signed-in role.
 *
 * @remarks
 * Shown when the API answers HTTP 501: the endpoint is in the contract but not built. It is not an
 * error, so it uses neutral colours and no alarm.
 */
export function NotAvailableState({ title, body, icon = 'clock', onCheckAgain }: NotAvailableStateProps) {
  return (
    <div className="state-card" role="status">
      <span className="state-icon" aria-hidden="true">
        <Icon name={icon} />
      </span>
      <div className="state-text">
        <b>{title ?? t('states.notAvailableTitle')}</b>
        <span>{body}</span>
        {onCheckAgain ? (
          <button type="button" className="btn" onClick={onCheckAgain}>
            {t('states.checkAgain')}
          </button>
        ) : null}
      </div>
    </div>
  )
}

/** Props for {@link ErrorState}. */
export interface ErrorStateProps {
  /** What could not be done, for example `We couldn't load your worklist`. */
  readonly title: string
  /** When the failed attempt was made; shown so people know how fresh the message is. */
  readonly failedAt?: Date
  /** Called when the person asks to try again; no button when absent. */
  readonly onRetry?: () => void
}

/**
 * A friendly error message with a way to try again.
 *
 * @remarks
 * Shown when the API fails in any way other than "not built yet": a network failure, a server error
 * or an unexpected answer. It is announced to screen readers straight away.
 */
export function ErrorState({ title, failedAt, onRetry }: ErrorStateProps) {
  return (
    <div className="state-card error" role="alert">
      <span className="state-icon" aria-hidden="true">
        <Icon name="alert" />
      </span>
      <div className="state-text">
        <b>{title}</b>
        <span>
          {t('states.errorBody')}
          {failedAt ? ` ${t('states.lastTried', { time: formatTime(failedAt) })}` : null}
        </span>
        {onRetry ? (
          <button type="button" className="btn" onClick={onRetry}>
            {t('states.tryAgain')}
          </button>
        ) : null}
      </div>
    </div>
  )
}

/** Props for {@link EmptyState}. */
export interface EmptyStateProps {
  /** The heading, for example `Nothing needs you right now`. */
  readonly title: string
  /** The explanation under the heading. */
  readonly body: string
  /** The icon: a green check when nothing being there is good news, a neutral search mark otherwise. */
  readonly icon?: 'positive' | 'neutral' | IconName
  /** The heading level; 2 by default, 1 when the message is the whole page. */
  readonly level?: 1 | 2
  /** An optional action, such as a button to show everything. */
  readonly action?: ReactNode
}

/**
 * A centred message with an icon, used instead of an empty list or a missing screen.
 *
 * @remarks
 * CareLoop never shows an empty table: it says what is on track, what to try next, or what is
 * missing.
 */
export function EmptyState({ title, body, icon = 'neutral', level = 2, action }: EmptyStateProps) {
  const Heading = level === 1 ? 'h1' : 'h2'
  const name: IconName = icon === 'positive' ? 'check' : icon === 'neutral' ? 'search' : icon
  const tone = icon === 'positive' ? 'big' : icon === 'neutral' ? 'big neutral' : 'big accent'
  return (
    <div className="empty">
      <span className={tone} aria-hidden="true">
        <Icon name={name} />
      </span>
      <Heading className="empty-title">{title}</Heading>
      <p>{body}</p>
      {action}
    </div>
  )
}

/** Props for {@link ReadView}. */
export interface ReadViewProps<T> {
  /** The read to follow, from one of the data hooks. */
  readonly read: Read<T>
  /** What is loading, for screen readers, for example `Loading the case`. */
  readonly loadingLabel: string
  /** The shape of the loading placeholder; `lines` by default. */
  readonly skeleton?: SkeletonShape
  /** Shown when the endpoint answers 501. */
  readonly notAvailable: string
  /** Heading shown when the read fails, for example `We couldn't load this case`. */
  readonly errorTitle: string
  /** Heading and text shown when the API answers 404; a 404 is treated as an error when absent. */
  readonly notFound?: { readonly title: string; readonly body: string; readonly action?: ReactNode }
  /** Renders the data once it has arrived. */
  readonly children: (data: T) => ReactNode
}

/**
 * Shows a read from the API in whichever state it is in: loading, data, not ready yet, not found or
 * failed with a way to try again.
 *
 * @remarks
 * Every screen uses it so the states look and behave the same everywhere.
 */
export function ReadView<T>({
  read,
  loadingLabel,
  skeleton = 'lines',
  notAvailable,
  errorTitle,
  notFound,
  children,
}: ReadViewProps<T>) {
  const { state, retry } = read
  if (state.status === 'loading') {
    return <LoadingState label={loadingLabel} shape={skeleton} />
  }
  if (state.status === 'ready') {
    return <>{children(state.data)}</>
  }
  if (state.failure.kind === 'notAvailable') {
    return <NotAvailableState body={notAvailable} onCheckAgain={retry} />
  }
  if (state.failure.kind === 'notFound' && notFound) {
    return <EmptyState title={notFound.title} body={notFound.body} action={notFound.action} />
  }
  return <ErrorState title={errorTitle} failedAt={state.failedAt} onRetry={retry} />
}
