/**
 * Shared components: the header, icons, status pills, the dialog and toast, the form that asks
 * why someone opens a case outside their team, the error boundary, the chip and captioned figure
 * that mark words AI wrote, and the loading, empty, "not ready yet", "not open to you" and error
 * states every screen uses.
 */

export { AccessReasonForm } from './AccessReasonForm'
export type { AccessReasonFormProps } from './AccessReasonForm'
export { AiChip, AiWords } from './AiWords'
export type { AiChipProps, AiWordsProps, AiWordsVersions } from './AiWords'
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
