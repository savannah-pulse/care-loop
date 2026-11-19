/**
 * Shared components: the header, icons, status pills, the dialog and toast, the form that asks
 * why someone opens a case outside their team, the AI-written summary, the error boundary, and the loading, empty, "not
 * ready yet", "not open to you" and error states every screen uses.
 */

export { AiSummary } from './AiSummary'
export type { AiSummaryProps } from './AiSummary'
export { AccessReasonForm } from './AccessReasonForm'
export type { AccessReasonFormProps } from './AccessReasonForm'
export { AppHeader } from './AppHeader'
export type { AppHeaderProps } from './AppHeader'
export { Dialog } from './Dialog'
export type { DialogProps } from './Dialog'
export { ErrorBoundary } from './ErrorBoundary'
export type { ErrorBoundaryProps } from './ErrorBoundary'
export { Icon, Logo } from './Icon'
export type { IconName, IconProps } from './Icon'
export { OutsideTeamCard } from './OutsideTeamCard'
export type { OutsideTeamCardProps } from './OutsideTeamCard'
export { Badge } from './Status'
export type { BadgeProps } from './Status'
export { EmptyState, ErrorState, LoadingState, NotAvailableState, ReadView } from './States'
export { TOAST_DURATION_MS, Toast } from './Toast'
export type { ToastProps } from './Toast'
export type {
  EmptyStateProps,
  ErrorStateProps,
  LoadingStateProps,
  NotAvailableStateProps,
  ReadViewProps,
  SkeletonShape,
} from './States'
