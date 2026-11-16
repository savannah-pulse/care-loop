import type { CaseSummary, ExceptionItem } from '../../api/client'
import { codeText, t } from '../../i18n'
import type { TextKey } from '../../i18n'
import { dateText } from '../../format'
import { sortCases } from './sortCases'

/** Coordination fields shared by a list row and an exception row. */
export type CaseRow = Pick<CaseSummary, 'caseId' | 'patientReference' | 'patientDisplayName'
  | 'patientSecondaryIdentifier' | 'pathwayId' | 'displayLabel' | 'nextAction' | 'owner' | 'dueAt'
  | 'exceptions' | 'urgency'>

/** Props for the read-only coordination worklist. */
export interface CaseTableProps {
  /** Current authorized rows, already filtered by the server or current-page controls. */
  rows: readonly (CaseRow | ExceptionItem)[]
  /** Opens a case, preserving the access reason for its subsequent reads. */
  onOpen: (id: string) => void
}

/** A read-only worklist: one next action, accountable owner and deadline, with worded status. */
export function CaseTable({ rows, onOpen }: CaseTableProps) {
  const headings: TextKey[] = ['common.patient', 'common.programme', 'common.stage', 'common.action', 'common.owner', 'common.due', 'common.status']
  if (rows.length === 0) return <p className="notice">{t('common.empty')}</p>
  return <div className="table-scroll"><table>
    <thead><tr>{headings.map((key) => <th key={key}>{t(key)}</th>)}</tr></thead>
    <tbody>{sortCases(rows).map((row) => <tr key={row.caseId}>
      <td><button className="link" onClick={() => onOpen(row.caseId)}>
        {row.patientDisplayName ?? row.caseId}</button><small>{row.patientSecondaryIdentifier
          ?? (row.patientDisplayName != null ? row.patientReference : null)}</small></td>
      <td>{row.pathwayId}</td><td>{codeText('label', row.displayLabel)}</td>
      <td>{row.nextAction == null ? t('common.noNextAction') : codeText('action', row.nextAction)}<small>{row.urgency ?? t('common.none')}</small></td>
      <td>{row.owner.reference}<small>{codeText('role', row.owner.role)}</small></td>
      <td>{row.dueAt == null ? t('common.noDeadline') : <time dateTime={row.dueAt}>{dateText(row.dueAt)}</time>}</td>
      <td>{row.exceptions.length ? row.exceptions.map((flag) =>
        <span className={`badge ${flag === 'Overdue' ? 'danger' : 'warning'}`} key={flag}>{codeText('exception', flag)}</span>)
        : <span className="badge success">{t('common.onTrack')}</span>}</td>
    </tr>)}</tbody>
  </table></div>
}
