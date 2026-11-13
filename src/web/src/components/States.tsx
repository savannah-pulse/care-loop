import type { ReactNode } from 'react'
import type { Read } from '../data'
import { formatTime } from '../format'
import { t } from '../i18n'
import { Icon, type IconName } from './Icon'

/** Props for {@link LoadingState}. */
export interface LoadingStateProps {
  /** What is loading, for example `Loading cases…`. */
  readonly text: string
}

/**
 * A calm line shown while data loads.
 *
 * @remarks
 * Announced politely to screen readers. The spinner stops for people who prefer reduced motion.
 */
export function LoadingState({ text }: LoadingStateProps) {
  return (
    <div className="state-line" role="status">
      <span className="spinner" aria-hidden="true" />
      {text}
    </div>
  )
}

/** Props for {@link NotAvailableState}. */
export interface NotAvailableStateProps {
  /** The heading; `Not available yet` by default. */
  readonly title?: string
  /** What is not available and what to expect. */
  readonly body: string
  /** The icon in front: a clock for "not yet" by default, a lock for "not for your role". */
  readonly icon?: IconName
  /** Called when the person asks to check again; no button when absent. */
  readonly onCheckAgain?: () => void
}

/**
 * A calm notice that a part of CareLoop is not available yet, or not for the signed-in role.
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
          <button type="button" className="btn small-btn" onClick={onCheckAgain}>
            {t('states.checkAgain')}
          </button>
        ) : null}
      </div>
    </div>
  )
}

/** Props for {@link ErrorState}. */
export interface ErrorStateProps {
  /** What could not be done, for example `Cases could not be loaded`. */
  readonly title: string
  /** When the failed attempt was made; shown so people know how fresh the message is. */
  readonly failedAt?: Date
  /** Called when the person asks to try again; no button when absent. */
  readonly onRetry?: () => void
}

/**
 * An error message with a way to try again.
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
          <button type="button" className="btn small-btn" onClick={onRetry}>
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
  /** Whether nothing being there is good news; shows a green check instead of a neutral mark. */
  readonly positive?: boolean
  /** An optional action, such as a button to show everything. */
  readonly action?: ReactNode
}

/**
 * A centred message used instead of an empty list or table.
 *
 * @remarks
 * CareLoop never shows an empty table: it says what is on track, what to try next, or what is
 * missing.
 */
export function EmptyState({ title, body, positive = false, action }: EmptyStateProps) {
  return (
    <div className="empty">
      <span className={positive ? 'big' : 'big neutral'} aria-hidden="true">
        <Icon name={positive ? 'check' : 'search'} />
      </span>
      <b>{title}</b>
      <span>{body}</span>
      {action}
    </div>
  )
}

/** Props for {@link ReadView}. */
export interface ReadViewProps<T> {
  /** The read to follow, from one of the data hooks. */
  readonly read: Read<T>
  /** Shown while loading, for example `Loading the case…`. */
  readonly loading: string
  /** Shown when the endpoint answers 501. */
  readonly notAvailable: string
  /** Heading shown when the read fails. */
  readonly errorTitle: string
  /** Heading and text shown when the API answers 404; a 404 is treated as an error when absent. */
  readonly notFound?: { readonly title: string; readonly body: string; readonly action?: ReactNode }
  /** Renders the data once it has arrived. */
  readonly children: (data: T) => ReactNode
}

/**
 * Shows a read from the API in whichever state it is in: loading, data, not available yet, not
 * found or failed with a way to try again.
 *
 * @remarks
 * Every screen uses it so the states look and behave the same everywhere.
 */
export function ReadView<T>({ read, loading, notAvailable, errorTitle, notFound, children }: ReadViewProps<T>) {
  const { state, retry } = read
  if (state.status === 'loading') {
    return <LoadingState text={loading} />
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
