/**
 * Shared components: the header, icons, status pills, the dialog and toast, and the loading,
 * empty, "not ready yet" and error states every screen uses.
 */

export { AppHeader } from './AppHeader'
export type { AppHeaderProps } from './AppHeader'
export { Dialog } from './Dialog'
export type { DialogProps } from './Dialog'
export { Icon, Logo } from './Icon'
export type { IconName, IconProps } from './Icon'
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
