import { Icon, OutsideTeamCard, ReadView } from '../../components'
import { useCase, useCaseTimeline, type CaseDetail, type Read, type TimelineEntry } from '../../data'
import { describePatient, openEscalations, programmeName, resultSourceText } from '../../domain'
import { facilityName, formatDate, parseInstant } from '../../format'
import { useDocumentTitle, useNow } from '../../hooks'
import { isClinicianRole, rememberAccessReason, useAccessReason, useSignedInStaff } from '../../identity'
import { t } from '../../i18n'
import { navigate, routeHref } from '../../routing'
import { EscalationLadder } from '../escalations'
import { NextActionCard } from './NextActionCard'
import { OtherScreeningCard } from './OtherScreeningCard'
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
  const me = useSignedInStaff()
  const patient = describePatient(detail, me.role)
  const opened = parseInstant(detail.createdAt)
  const meta: MetaItem[] = []
  if (patient.identifier && !patient.hidden) {
    meta.push({ text: patient.identifier })
  }
  meta.push(
    { text: programmeName(detail.pathwayId), title: detail.pathwayId },
    { text: facilityName(detail.facilityId), title: detail.facilityId },
  )
  if (opened) {
    meta.push({ text: t('case.opened', { date: formatDate(opened) }) })
  }
  return (
    <div className="patient-head">
      <span className="pi" aria-hidden="true">
        <Icon name="user" />
      </span>
      <div className="who">
        <h1 title={patient.reference}>{patient.name}</h1>
        <p className="meta">
          {meta.map((item) => (
            <span key={item.text} title={item.title}>
              {item.text}
            </span>
          ))}
        </p>
        <p className="quiet" title={detail.sourceSystem}>
          {resultSourceText(detail.sourceResultId, me.role)}
        </p>
        <p className="quiet">{patient.hidden ? t('patient.hiddenDetail') : t('patient.detail')}</p>
      </div>
    </div>
  )
}

interface CaseViewProps {
  readonly detail: CaseDetail
  readonly timeline: Read<TimelineEntry[]>
  readonly reason?: string
  readonly now: Date
}

function CaseView({ detail, timeline, reason, now }: CaseViewProps) {
  const me = useSignedInStaff()
  const escalated = openEscalations(detail.escalations).length > 0
  return (
    <>
      <PatientHeader detail={detail} />
      <StageStepper label={detail.displayLabel} />
      <div className="case-grid">
        <NextActionCard detail={detail} now={now}>
          {escalated ? (
            <EscalationLadder
              detail={detail}
              timeline={timeline.state.status === 'ready' ? timeline.state.data : undefined}
              now={now}
            />
          ) : null}
        </NextActionCard>
        <div className="side">
          <TracksCard tracks={detail.tracks} />
          {isClinicianRole(me.role) ? <OtherScreeningCard detail={detail} reason={reason} /> : null}
        </div>
        <TimelineCard read={timeline} />
      </div>
    </>
  )
}

/**
 * Screen 2, a case: who the patient is, where the case is, the one next action with who is
 * responsible, when it is due and how its escalation stands, how each part is going, the patient's
 * other screening for clinicians, and what has happened.
 *
 * @remarks
 * Read-only; reads `GET /cases/{caseId}` and `GET /cases/{caseId}/timeline`. Staff see the patient's
 * name and MRN; the programme coordinator sees a case number and a note that names and hospital
 * record numbers are hidden for their role, and the result reads as coming from the hospital
 * system, without its number. A case at another facility opens only after the person says why
 * (board 14): the API refuses the read without a reason, the screen asks for one, and the reason
 * then goes with every read of the case. Any other refusal says calmly that the case is not open to
 * the person. Clinicians reach the AI handoff summary from the next action when a referral is due.
 */
export function CasePage({ caseId }: CasePageProps) {
  const me = useSignedInStaff()
  const reason = useAccessReason(caseId)
  const read = useCase(caseId, reason)
  const timeline = useCaseTimeline(caseId, reason)
  const now = useNow()
  const ready = read.state.status === 'ready'
  useDocumentTitle(
    read.state.status === 'ready' ? describePatient(read.state.data, me.role).name : t('case.titleFallback'),
  )
  const askReason =
    read.state.status === 'failed' && read.state.failure.kind === 'forbidden' && !reason && me.facilityId !== undefined
  return (
    <div className="page">
      <a className="crumb" href={routeHref({ name: 'worklist' })}>
        <Icon name="left" />
        {t('case.back')}
      </a>
      {ready ? null : <h1 className="visually-hidden">{t('case.titleFallback')}</h1>}
      {askReason ? (
        <OutsideTeamCard
          onReason={(text) => rememberAccessReason(caseId, text)}
          onCancel={() => navigate({ name: 'worklist' })}
        />
      ) : (
        <ReadView
          read={read}
          loadingLabel={t('case.loading')}
          skeleton="page"
          notAvailable={t('case.notAvailable')}
          errorTitle={t('case.errorTitle')}
          notFound={{ title: t('case.notFoundTitle'), body: t('case.notFoundBody') }}
          forbidden={{
            title: t('case.forbiddenTitle'),
            body: me.role === 'coordinator' ? t('case.forbiddenCoordinator') : t('case.forbiddenBody'),
          }}
        >
          {(detail) => <CaseView detail={detail} timeline={timeline} reason={reason} now={now} />}
        </ReadView>
      )}
      <p className="note">{t('case.note')}</p>
    </div>
  )
}
