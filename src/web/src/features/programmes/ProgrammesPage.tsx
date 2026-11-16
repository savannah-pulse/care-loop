import { useId, useRef, useState } from 'react'
import { Badge, Dialog, Icon, ReadView } from '../../components'
import { useProgramme, useProgrammes, type Programme } from '../../data'
import { nextActionText, urgencyText } from '../../domain'
import { formatDate, parseInstant } from '../../format'
import { useDocumentTitle, useOverflowsSideways } from '../../hooks'
import { roleLabel } from '../../identity'
import { t } from '../../i18n'
import { routeHref } from '../../routing'
import {
  deadlineKey,
  evidenceKindName,
  isZeroDuration,
  ladderInOrder,
  programmeApproval,
  settingDuration,
  distinctCitations,
  settingText,
  sharedCitation,
} from './programmes'

/** Props for {@link ProgrammesPage}. */
export interface ProgrammesPageProps {
  /** The programme to show, from the address; the first one when absent. */
  readonly pathwayId?: string
}

function roleWords(role: string | null | undefined): string {
  return role ? (roleLabel(role) ?? role) : t('programmesPage.ownerNotSet')
}

function ApprovalLine({ programme }: { readonly programme: Programme }) {
  const approval = programmeApproval(programme)
  return (
    <span className={approval === 'approved' ? 'dotline green' : 'dotline amber'}>
      {t(`programmesPage.approval.${approval}`)}
    </span>
  )
}

function ChangeHistory({ programme }: { readonly programme: Programme }) {
  const [open, setOpen] = useState(false)
  const changes = [...programme.changeLog].reverse()
  return (
    <>
      <button type="button" className="btn" onClick={() => setOpen(true)}>
        {t('programmesPage.viewHistory')}
      </button>
      <Dialog open={open} title={t('programmesPage.historyTitle')} onClose={() => setOpen(false)}>
        {changes.length === 0 ? (
          <p>{t('programmesPage.historyEmpty')}</p>
        ) : (
          <ol className="history">
            {changes.map((change) => {
              const date = parseInstant(change.date)
              return (
                <li key={`${change.version}-${change.date}`}>
                  <b>{t('programmesPage.historyVersion', { version: change.version })}</b>
                  <span className="sub-line">
                    {[date ? formatDate(date) : change.date, settingText(change.approver).text].join(t('common.separator'))}
                  </span>
                  <p>{change.summary}</p>
                </li>
              )
            })}
          </ol>
        )}
        <div className="dialog-actions">
          <button type="button" className="btn" onClick={() => setOpen(false)}>
            {t('programmesPage.close')}
          </button>
        </div>
      </Dialog>
    </>
  )
}

interface SourcesProps {
  readonly sources: readonly string[]
  readonly shared?: string
  readonly forAll: 'programmesPage.sourceForAll' | 'programmesPage.rulesSourceForAll'
}

function Sources({ sources, shared, forAll }: SourcesProps) {
  if (shared) {
    return <p className="note cite">{t(forAll, { source: settingText(shared).text })}</p>
  }
  if (sources.length === 0) {
    return null
  }
  return (
    <ol className="sources note">
      {sources.map((source, index) => (
        <li key={source} className="cite">
          {t('programmesPage.sourceItem', { n: index + 1, source: settingText(source).text })}
        </li>
      ))}
    </ol>
  )
}

function StepsCard({ programme }: { readonly programme: Programme }) {
  const headingId = useId()
  const latest = programme.changeLog.at(-1)
  const since = latest ? parseInstant(latest.date) : undefined
  const allWaiting = programme.deadlines.length > 0 && programme.deadlines.every((deadline) => !deadline.clinicallyApproved)
  const name = settingText(programme.displayName)
  const shared = sharedCitation(programme.deadlines.map((deadline) => deadline.citation))
  const sources = distinctCitations(programme.deadlines.map((deadline) => deadline.citation))
  const scrollRef = useRef<HTMLDivElement>(null)
  const scrolls = useOverflowsSideways(scrollRef)
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {name.text}
        <span className="r">
          {programme.isPlaceholder || name.placeholder ? (
            <Badge tone="amber">{t('programmesPage.placeholder')}</Badge>
          ) : (
            <Badge tone="green">{t('programmesPage.inUse')}</Badge>
          )}
        </span>
      </h2>
      <p className="why">
        {since
          ? t('programmesPage.versionSince', { version: programme.version, date: formatDate(since) })
          : t('programmesPage.version', { version: programme.version })}{' '}
        {t('programmesPage.clockTime')}
        {allWaiting ? ` ${t('programmesPage.allWaiting')}` : null}
      </p>
      <div
        className="table-scroll"
        ref={scrollRef}
        tabIndex={scrolls ? 0 : undefined}
        role={scrolls ? 'region' : undefined}
        aria-label={scrolls ? t('programmesPage.stepsCaption') : undefined}
      >
        <table className="mini steps">
          <caption className="visually-hidden">{t('programmesPage.stepsCaption')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('programmesPage.columns.step')}</th>
              <th scope="col">{t('programmesPage.columns.deadline')}</th>
              <th scope="col">{t('programmesPage.columns.reminder')}</th>
              <th scope="col">{t('programmesPage.columns.owner')}</th>
            </tr>
          </thead>
          <tbody>
            {programme.deadlines.map((deadline) => (
              <tr key={deadlineKey(deadline)}>
                <th scope="row">
                  {nextActionText(deadline.nextAction)}
                  {deadline.urgencyCode ? (
                    <span className="l2">
                      {t('programmesPage.forUrgency', { urgency: urgencyText(deadline.urgencyCode) })}
                    </span>
                  ) : null}
                </th>
                <td>
                  {settingDuration(deadline.duration)}
                  {deadline.clinicallyApproved || allWaiting ? null : (
                    <span className="l2">
                      <Badge tone="amber">{t('programmesPage.waiting')}</Badge>
                    </span>
                  )}
                  {deadline.citation && !shared ? (
                    <span className="l2">{t('programmesPage.sourceRef', { n: sources.indexOf(deadline.citation) + 1 })}</span>
                  ) : null}
                </td>
                <td>{t('programmesPage.before', { duration: settingDuration(deadline.warnBefore) })}</td>
                <td>{roleWords(deadline.ownerRole)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Sources sources={sources} shared={shared} forAll="programmesPage.sourceForAll" />
    </section>
  )
}

function LateCard({ programme }: { readonly programme: Programme }) {
  const headingId = useId()
  const ladder = ladderInOrder(programme.escalationLadder)
  const allWaiting = ladder.length > 0 && ladder.every((step) => !step.clinicallyApproved)
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('programmesPage.lateTitle')}
      </h2>
      <ol className="late">
        <li>
          <span className="rung-n" aria-hidden="true">
            1
          </span>
          <b>{t('programmesPage.lateOwner')}</b>
          <span className="r">{t('programmesPage.lateReminder')}</span>
        </li>
        {ladder.map((step, index) => (
          <li key={Number(step.level)}>
            <span className="rung-n" aria-hidden="true">
              {index + 2}
            </span>
            <b>{roleWords(step.escalateToRole)}</b>
            <span className="r">
              {isZeroDuration(step.afterOverdue)
                ? t('programmesPage.atDeadline')
                : t('programmesPage.afterDeadline', { duration: settingDuration(step.afterOverdue) })}
              {step.clinicallyApproved || allWaiting ? null : (
                <>
                  {' '}
                  <Badge tone="amber">{t('programmesPage.waiting')}</Badge>
                </>
              )}
            </span>
          </li>
        ))}
      </ol>
      <p className="note linkrow">
        {t('programmesPage.lateNote')}
        {allWaiting ? ` ${t('programmesPage.ladderWaiting')}` : null}
      </p>
    </section>
  )
}

function EvidenceCard({ programme }: { readonly programme: Programme }) {
  const headingId = useId()
  const allWaiting = programme.evidenceKinds.length > 0 && programme.evidenceKinds.every((kind) => !kind.clinicallyApproved)
  const shared = sharedCitation(programme.closureRules.map((rule) => rule.citation))
  const sources = distinctCitations(programme.closureRules.map((rule) => rule.citation))
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('programmesPage.evidenceTitle')}
        {allWaiting ? (
          <span className="r">
            <Badge tone="amber">{t('programmesPage.waiting')}</Badge>
          </span>
        ) : null}
      </h2>
      <ul className="rules">
        {programme.evidenceKinds.map((kind) => (
          <li key={kind.kind}>
            <span className="rule-head">
              <b>{evidenceKindName(kind.kind)}</b>
              {kind.clinicallyApproved || allWaiting ? null : <Badge tone="amber">{t('programmesPage.waiting')}</Badge>}
            </span>
            {kind.description ? <span className="sub-line">{settingText(kind.description).text}</span> : null}
            {kind.nextAction ? (
              <span className="sub-line">
                {t('programmesPage.evidenceNext', { action: nextActionText(kind.nextAction) })}
              </span>
            ) : null}
          </li>
        ))}
      </ul>
      <details className="disclosure rules-more">
        <summary>
          {t('programmesPage.closureTitle', { count: programme.closureRules.length })}
          <Icon name="down" />
        </summary>
        <ul className="rules">
        {programme.closureRules.map((rule) => {
          const needs = [
            rule.requiresEvidenceReference ? t('programmesPage.needs.evidence') : '',
            rule.requiresExceptionNote ? t('programmesPage.needs.note') : '',
            rule.requiresNextSurveillanceDate ? t('programmesPage.needs.date') : '',
            Number(rule.minimumDocumentedAttempts) > 0
              ? t('programmesPage.needs.attempts', { count: Number(rule.minimumDocumentedAttempts) })
              : '',
          ].filter((need) => need !== '')
          return (
            <li key={String(rule.reason)}>
              <span className="rule-head">
                <b>{rule.reason ? t(`closureReasons.${rule.reason}`) : t('programmesPage.unknownReason')}</b>
                {rule.isException ? <Badge tone="grey">{t('programmesPage.exception')}</Badge> : null}
              </span>
              <span className="sub-line">
                {t('programmesPage.closedBy', {
                  roles: rule.authorizedRoles.map((role) => roleWords(role)).join(t('programmesPage.or')),
                })}
              </span>
              {needs.length > 0 ? (
                <span className="sub-line">{t('programmesPage.needsList', { needs: needs.join(t('common.separator')) })}</span>
              ) : null}
              {rule.citation && !shared ? (
                <span className="sub-line">{t('programmesPage.sourceRef', { n: sources.indexOf(rule.citation) + 1 })}</span>
              ) : null}
            </li>
          )
        })}
        </ul>
        <Sources sources={sources} shared={shared} forAll="programmesPage.rulesSourceForAll" />
      </details>
    </section>
  )
}

function ProgrammeDetail({ pathwayId }: { readonly pathwayId: string }) {
  const read = useProgramme(pathwayId)
  return (
    <ReadView
      read={read}
      loadingLabel={t('programmesPage.loadingOne')}
      skeleton="page"
      notAvailable={t('programmesPage.notAvailableBody')}
      errorTitle={t('programmesPage.errorTitle')}
      notFound={{ title: t('programmesPage.notFoundTitle'), body: t('programmesPage.notFoundBody') }}
    >
      {(programme) => (
        <div className="stack">
          <StepsCard programme={programme} />
          <div className="grid-even">
            <LateCard programme={programme} />
            <EvidenceCard programme={programme} />
          </div>
          <div className="history-row">
            <ChangeHistory programme={programme} />
          </div>
        </div>
      )}
    </ReadView>
  )
}

function ProgrammeList({ programmes, selected }: { readonly programmes: readonly Programme[]; readonly selected: string }) {
  return (
    <nav className="prog-list" aria-label={t('programmesPage.listLabel')}>
      <ul>
        {programmes.map((programme) => (
          <li key={programme.pathwayId}>
            <a
              href={routeHref({ name: 'programmes', pathwayId: programme.pathwayId })}
              aria-current={programme.pathwayId === selected ? 'page' : undefined}
            >
              <b>{settingText(programme.displayName).text}</b>
              <span className="sub-line">{t('programmesPage.version', { version: programme.version })}</span>
              <ApprovalLine programme={programme} />
            </a>
          </li>
        ))}
      </ul>
    </nav>
  )
}

/**
 * Screen 4, programmes: each screening programme's steps with their deadline, reminder and owner
 * role, what happens when a step is late, the evidence and closure rules, and the change history
 * (C1).
 *
 * @remarks
 * Read-only. Reads `GET /programmes` for the list and `GET /programmes/{pathwayId}` for the one on
 * screen, which the address names (`#/programmes/{pathwayId}`). Values a clinical adviser has not
 * approved say "Waiting for approval", and a placeholder definition says "Placeholder", in words
 * instead of the settings' capitalised placeholder marker. A citation shared by every deadline or
 * rule is shown once. Durations are clock time. Changing a programme happens outside CareLoop.
 */
export function ProgrammesPage({ pathwayId }: ProgrammesPageProps) {
  useDocumentTitle(t('programmesPage.title'))
  const list = useProgrammes()
  const count = list.state.status === 'ready' ? list.state.data.length : undefined
  return (
    <div className="page">
      <div className="page-head">
        <h1>{t('programmesPage.title')}</h1>
        {count !== undefined ? <p className="lede">{t('programmesPage.lede', { count })}</p> : null}
      </div>
      <ReadView
        read={list}
        loadingLabel={t('programmesPage.loading')}
        skeleton="page"
        notAvailable={t('programmesPage.notAvailableBody')}
        errorTitle={t('programmesPage.errorTitle')}
      >
        {(programmes) => {
          const selected = pathwayId ?? programmes[0]?.pathwayId
          if (!selected) {
            return (
              <div className="empty">
                <span className="big neutral" aria-hidden="true">
                  <Icon name="list" />
                </span>
                <h2 className="empty-title">{t('programmesPage.emptyTitle')}</h2>
                <p>{t('programmesPage.emptyBody')}</p>
              </div>
            )
          }
          return (
            <div className="prog-grid">
              <ProgrammeList programmes={programmes} selected={selected} />
              <ProgrammeDetail key={selected} pathwayId={selected} />
            </div>
          )
        }}
      </ReadView>
      <p className="note">{t('programmesPage.note')}</p>
    </div>
  )
}
