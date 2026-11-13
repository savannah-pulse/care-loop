import { hasMessage, t } from '../i18n'

/**
 * Names a screening programme from its pathway identifier.
 *
 * @param pathwayId - The pathway the case follows, for example `breast-screening`.
 * @returns The programme's name, for example `Breast screening`, or the identifier when the
 * dashboard has no name for it.
 *
 * @remarks
 * Programme names belong to the pathway definitions, which the API does not share yet
 * (`GET /programmes`, C1). Until it does, the names are in the language catalogue.
 */
export function programmeName(pathwayId: string): string {
  const key = `programmes.${pathwayId}`
  return hasMessage(key) ? t(key) : pathwayId
}
