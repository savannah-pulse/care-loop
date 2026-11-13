import { Icon, ReadView } from '../../components'
import { useCase, useCaseTimeline, type CaseDetail, type Read, type TimelineEntry } from '../../data'
import { programmeName } from '../../domain'
import { facilityName, formatDate, parseInstant, patientLabel } from '../../format'
import { useDocumentTitle, useNow } from '../../hooks'
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

interface MetaItem {
  readonly text: string
  readonly title?: string
}

function PatientHeader({ detail }: { readonly detail: CaseDetail }) {
  const opened = parseInstant(detail.createdAt)
  const meta: MetaItem[] = [
    { text: programmeName(detail.pathwayId), title: detail.pathwayId },
    { text: facilityName(detail.facilityId), title: detail.facilityId },
  ]
  if (opened) {
    meta.push({ text: t('case.opened', { date: formatDate(opened) }) })
  }
  return (
    <div className="patient-head">
      <span className="pi" aria-hidden="true">
        <Icon name="user" />
      </span>
      <div className="who">
        <h1 title={detail.patientReference}>{patientLabel(detail.patientReference)}</h1>
        <p className="meta">
          {meta.map((item) => (
            <span key={item.text} title={item.title}>
              {item.text}
            </span>
          ))}
        </p>
        <p className="quiet" title={detail.sourceSystem}>
          {t('case.resultFrom', { result: detail.sourceResultId })}
        </p>
        <p className="quiet">{t('patient.detail')}</p>
      </div>
    </div>
  )
}

interface CaseViewProps {
  readonly detail: CaseDetail
  readonly timeline: Read<TimelineEntry[]>
  readonly now: Date
}

function CaseView({ detail, timeline, now }: CaseViewProps) {
  return (
    <>
      <PatientHeader detail={detail} />
      <StageStepper stage={detail.stage} tracks={detail.tracks} />
      <div className="case-grid">
        <NextActionCard detail={detail} now={now} />
        <div className="side">
          <TracksCard tracks={detail.tracks} />
        </div>
        <TimelineCard read={timeline} />
      </div>
    </>
  )
}

/**
 * Screen 2, a case: who the patient is (by number), where the case is, the one next action with who
 * is responsible, when it is due and whether it was escalated, how each part is going, and what has
 * happened.
 *
 * @remarks
 * Read-only; reads `GET /cases/{caseId}` and `GET /cases/{caseId}/timeline`. Case reads carry no
 * patient name, so the page title is the patient's number, with the full reference kept in a
 * tooltip. Clinicians reach the AI handoff summary from the next action when a referral is due.
 */
export function CasePage({ caseId }: CasePageProps) {
  const read = useCase(caseId)
  const timeline = useCaseTimeline(caseId)
  const now = useNow()
  const ready = read.state.status === 'ready'
  useDocumentTitle(
    read.state.status === 'ready' ? patientLabel(read.state.data.patientReference) : t('case.titleFallback'),
  )
  return (
    <div className="page">
      <a className="crumb" href={routeHref({ name: 'worklist' })}>
        <Icon name="left" />
        {t('case.back')}
      </a>
      {ready ? null : <h1 className="visually-hidden">{t('case.titleFallback')}</h1>}
      <ReadView
        read={read}
        loadingLabel={t('case.loading')}
        skeleton="page"
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
