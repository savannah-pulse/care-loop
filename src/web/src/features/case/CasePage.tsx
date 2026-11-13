import { useId } from 'react'
import { Icon, ReadView } from '../../components'
import { useCase, useCaseTimeline, type CaseDetail, type Read, type TimelineEntry } from '../../data'
import { programmeName, urgencyText } from '../../domain'
import { formatDateTimeWithYear, formatWhen, parseInstant } from '../../format'
import { useDocumentTitle, useNow } from '../../hooks'
import { isClinicianRole, useSignedInStaff } from '../../identity'
import { t } from '../../i18n'
import { routeHref } from '../../routing'
import { NextActionCard } from './NextActionCard'
import { StageStepper } from './StageStepper'
import { TimelineCard } from './TimelineCard'
import { TracksCard } from './TracksCard'

/** Props for {@link CasePage}. */
export interface CasePageProps {
  /** CareLoop's identifier of the case, from the address. */
  readonly caseId: string
}

interface CaseViewProps {
  readonly detail: CaseDetail
  readonly timeline: Read<TimelineEntry[]>
  readonly now: Date
}

function CaseBar({ detail, now }: { readonly detail: CaseDetail; readonly now: Date }) {
  const opened = parseInstant(detail.createdAt)
  const changed = parseInstant(detail.updatedAt)
  return (
    <div className="casebar">
      <span>
        {t('case.programme')} <b>{programmeName(detail.pathwayId)}</b>
      </span>
      <span>
        {t('case.urgency')} <b>{urgencyText(detail.urgency)}</b>
      </span>
      {opened ? (
        <span>
          {t('case.opened')} <b>{formatDateTimeWithYear(opened)}</b>
        </span>
      ) : null}
      {changed ? (
        <span>
          {t('case.lastChange')} <b>{formatWhen(changed, now)}</b>
        </span>
      ) : null}
      {detail.closureReason ? (
        <span>
          {t('case.closedReason')} <b>{t(`closureReasons.${detail.closureReason}`)}</b>
        </span>
      ) : null}
      {detail.outcomeEvidenceReference ? (
        <span>
          {t('case.outcomeEvidence')} <b>{detail.outcomeEvidenceReference}</b>
        </span>
      ) : null}
    </div>
  )
}

function SummaryCard({ caseId }: { readonly caseId: string }) {
  const headingId = useId()
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        <span className="ai">
          <Icon name="spark" />
          {t('summary.aiDraft')}
        </span>
        {t('case.summaryTitle')}
      </h2>
      <p className="note">{t('case.summaryBody')}</p>
      <a className="btn primary linkrow" href={routeHref({ name: 'summary', caseId })}>
        {t('case.summaryLink')}
      </a>
    </section>
  )
}

function CaseView({ detail, timeline, now }: CaseViewProps) {
  const me = useSignedInStaff()
  return (
    <>
      <div className="pbanner">
        <span className="av" aria-hidden="true">
          <Icon name="user" />
        </span>
        <div className="who">
          <h1 className="nm">{detail.patientReference}</h1>
          <p className="id">
            <span>{t('case.resultFrom', { result: detail.sourceResultId, system: detail.sourceSystem })}</span>
            <span>{t('case.facility', { facility: detail.facilityId })}</span>
          </p>
        </div>
      </div>
      <div className="card">
        <CaseBar detail={detail} now={now} />
        <StageStepper stage={detail.stage} tracks={detail.tracks} />
      </div>
      <div className="grid2">
        <div className="stack">
          <NextActionCard detail={detail} now={now} />
          <TimelineCard read={timeline} />
        </div>
        <div className="stack">
          <TracksCard tracks={detail.tracks} />
          {isClinicianRole(me.role) ? <SummaryCard caseId={detail.caseId} /> : null}
        </div>
      </div>
    </>
  )
}

/**
 * Screen 2, a case: where it is, what is late, who must act next and what has happened.
 *
 * @remarks
 * Read-only; reads `GET /cases/{caseId}` and `GET /cases/{caseId}/timeline`. Clinicians also get a
 * link to the AI handoff summary, which they check and approve on screen 18. Case reads carry the
 * patient reference only, so the reference is the page title.
 */
export function CasePage({ caseId }: CasePageProps) {
  const read = useCase(caseId)
  const timeline = useCaseTimeline(caseId)
  const now = useNow()
  useDocumentTitle(read.state.status === 'ready' ? read.state.data.patientReference : t('case.titleFallback'))
  return (
    <div className="page">
      <a className="crumb" href={routeHref({ name: 'worklist' })}>
        <Icon name="left" />
        {t('case.back')}
      </a>
      <ReadView
        read={read}
        loading={t('case.loading')}
        notAvailable={t('case.notAvailable')}
        errorTitle={t('case.errorTitle')}
        notFound={{ title: t('case.notFoundTitle'), body: t('case.notFoundBody') }}
      >
        {(detail) => <CaseView detail={detail} timeline={timeline} now={now} />}
      </ReadView>
      <p className="note">{t('case.note')}</p>
    </div>
  )
}
