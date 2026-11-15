import { Component, type ErrorInfo, type ReactNode } from 'react'
import { t } from '../i18n'
import { ErrorState } from './States'

/** Props for {@link ErrorBoundary}. */
export interface ErrorBoundaryProps {
  /** The screen it protects. */
  readonly children: ReactNode
}

interface ErrorBoundaryState {
  readonly failed: boolean
}

/**
 * Catches a screen that fails while showing what the API sent, and says so calmly instead of
 * leaving a blank page.
 *
 * @remarks
 * Carried over from the first dashboard on `feature/ws_5_6`: an unexpected answer must never look
 * like an empty or successful screen. The app gives it a new key for each screen and person, so
 * moving on starts afresh; "Try again" shows the screen again.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  /** Whether the screen failed. */
  override state: ErrorBoundaryState = { failed: false }

  /**
   * Notes that the screen failed, so the next render shows the error.
   *
   * @returns The failed state.
   */
  static getDerivedStateFromError(): ErrorBoundaryState {
    return { failed: true }
  }

  /**
   * Reports the failure to the browser console for whoever is debugging.
   *
   * @param error - What was thrown.
   * @param info - Where in the screen it was thrown.
   */
  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    console.error(error, info.componentStack)
  }

  /**
   * Shows the screen, or the error with a way to try again.
   *
   * @returns The screen or the error state.
   */
  override render(): ReactNode {
    if (this.state.failed) {
      return (
        <div className="page">
          <ErrorState
            title={t('states.screenFailed')}
            body={t('states.screenFailedBody')}
            onRetry={() => this.setState({ failed: false })}
          />
        </div>
      )
    }
    return this.props.children
  }
}
