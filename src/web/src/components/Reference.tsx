/** Props for {@link Reference}. */
export interface ReferenceProps {
  /** A source-system reference, for example `Patient/p-100245891`. */
  readonly value: string
}

/**
 * A source-system reference that wraps only after its type, never inside its identifier.
 *
 * @remarks
 * References such as `Patient/p-100245891` stand in for names until case reads carry a display name
 * (C20). In narrow table columns they break after `Patient/`, so the identifier stays whole and
 * readable. Copying the text gives the reference unchanged.
 */
export function Reference({ value }: ReferenceProps) {
  const slash = value.lastIndexOf('/')
  if (slash <= 0 || slash === value.length - 1) {
    return <span className="nowrap">{value}</span>
  }
  return (
    <>
      {value.slice(0, slash + 1)}
      <wbr />
      <span className="nowrap">{value.slice(slash + 1)}</span>
    </>
  )
}
