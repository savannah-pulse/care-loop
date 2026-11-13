/**
 * Shared components: the header, icons, status words and badges, and the loading, empty, "not
 * available yet" and error states every screen uses.
 */

export { AppHeader } from './AppHeader'
export type { AppHeaderProps } from './AppHeader'
export { Icon, Logo } from './Icon'
export type { IconName, IconProps } from './Icon'
export { Reference } from './Reference'
export type { ReferenceProps } from './Reference'
export { Badge, StatusText } from './Status'
export type { BadgeProps, StatusTextProps } from './Status'
export { EmptyState, ErrorState, LoadingState, NotAvailableState, ReadView } from './States'
export type {
  EmptyStateProps,
  ErrorStateProps,
  LoadingStateProps,
  NotAvailableStateProps,
  ReadViewProps,
} from './States'
