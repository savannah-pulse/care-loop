import { useId } from 'react'
import { Icon, ReadView } from '../../components'
import { usePatientCases, type CaseDetail } from '../../data'
import { displayLabelText, programmeName } from '../../domain'
import { formatDateWithYear, parseInstant } from '../../format'
import { rememberAccessReason } from '../../identity'
import { t } from '../../i18n'
import { routeHref } from '../../routing'

/** Props for {@link OtherScreeningCard}. */
export interface OtherScreeningCardProps {
  /** The case being looked at. */
  readonly detail: CaseDetail
  /** The reason given for a case outside the reader's team, sent with the search too. */
  readonly reason?: string
}

/**
 * "Hessa Al Dhaheri's other screening": the patient's other cases in CareLoop, for clinicians (C19,
 * board 18).
 *
 * @remarks
 * Reads `GET /cases?patientReference=…` with exactly the reference the API returned for this case,
 * in the case's facility (with the same reason when it is outside the reader's team). Each other
 * case shows its programme, when it was opened, where it is or why it closed, and links to it. Only
 * clinician roles see this card, and the API refuses the search to anyone else. The full history
 * stays in the hospital record.
 */
export function OtherScreeningCard({ detail, reason }: OtherScreeningCardProps) {
  const headingId = useId()
  const read = usePatientCases(detail.patientReference, detail.facilityId, reason)
  const name = detail.patientDisplayName?.trim()
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {name ? t('otherCases.titleNamed', { name }) : t('otherCases.title')}
      </h2>
      <ReadView
        read={read}
        loadingLabel={t('otherCases.loading')}
        notAvailable={t('otherCases.notAvailable')}
        errorTitle={t('otherCases.error')}
        forbidden={{ title: t('otherCases.forbiddenTitle'), body: t('otherCases.forbiddenBody') }}
      >
        {(list) => {
          const others = list.cases.filter((other) => other.caseId !== detail.caseId)
          if (others.length === 0) {
            return <p className="note">{t('otherCases.none')}</p>
          }
          return (
            <ul className="others">
              {others.map((other) => {
                const opened = parseInstant(other.createdAt)
                const where = other.closureReason
                  ? t('otherCases.closedAs', { reason: t(`closureReasons.${other.closureReason}`) })
                  : displayLabelText(other.displayLabel)
                return (
                  <li key={other.caseId}>
                    <Icon name="doc" />
                    <span className="other-text">
                      <a
                        href={routeHref({ name: 'case', caseId: other.caseId })}
                        onClick={reason ? () => rememberAccessReason(other.caseId, reason) : undefined}
                      >
                        {opened
                          ? t('otherCases.item', { programme: programmeName(other.pathwayId), date: formatDateWithYear(opened) })
                          : programmeName(other.pathwayId)}
                      </a>
                      <span className="sub-line">{where}</span>
                    </span>
                  </li>
                )
              })}
            </ul>
          )
        }}
      </ReadView>
      <p className="note linkrow">{t('otherCases.note')}</p>
    </section>
  )
}
