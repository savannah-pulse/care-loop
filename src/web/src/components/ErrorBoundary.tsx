import { Component } from 'react'
import type { PropsWithChildren } from 'react'
import { t } from '../i18n'

/** Displays malformed-data rendering failures explicitly instead of crashing or pretending the data is empty. */
export class ErrorBoundary extends Component<PropsWithChildren, { message: string | null }> {
  /** Current explicit rendering failure, if any. */
  state: { message: string | null } = { message: null }

  /**
   * Captures a render failure for a visible localized notice.
   * @param error - The thrown rendering error.
   * @returns The error-state update.
   */
  static getDerivedStateFromError(error: unknown) {
    return { message: error instanceof Error ? error.message : t('error.display') }
  }

  /** Shows authorized child content or an explicit failure with reload recovery. */
  render() {
    return this.state.message ? <section><p role="alert" className="notice error">{this.state.message}</p>
      <button onClick={() => window.location.reload()}>{t('common.refresh')}</button></section> : this.props.children
  }
}
