import { hasMessage, t } from '../i18n'

/**
 * Names a screening programme from its pathway identifier.
 *
 * @param pathwayId - The pathway the case follows, for example `breast-screening`.
 * @returns The programme's name, for example `Breast screening`, or the identifier when the
 * dashboard has no name for it.
 *
 * @remarks
 * Lists and case screens use the short name in the language catalogue, such as `Breast screening`.
 * The Programmes screen shows each programme's configured name from `GET /programmes`, which can be
 * longer and say that the settings are placeholders.
 */
export function programmeName(pathwayId: string): string {
  const key = `programmes.${pathwayId}`
  return hasMessage(key) ? t(key) : pathwayId
}
