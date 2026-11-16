import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ActionResult, SimulatedCase } from '../../api/simulator';
import { useSimulatorAction } from '../../api/useSimulatorAction';
import { useSimulatorRead } from '../../api/useSimulatorRead';
import { ApiStatus } from '../../components/ApiStatus';

/** Props for explicit human source actions. */
export interface ClinicianActionsPageProps {
  /** Optional simulator API base URL; omission uses the current origin. */
  apiBaseUrl?: string;
}
const actions = [
  'clinical-review',
  'consultation-request',
  'consultation-answer',
  'referral',
  'referral-decision',
  'appointment',
  'outcome',
  'closure',
  'consent',
] as const;
const roles = [
  'clinician',
  'receiving-clinician',
  'facility-lead',
  'nurse-navigator',
  'coordinator',
  'lab',
] as const;
const closureReasons = [
  'treated',
  'on-surveillance',
  'negative-on-confirmation',
  'declined',
  'transferred-and-accepted',
  'untraceable',
] as const;
const declineReasons = ['noCapacity', 'wrongService', 'missingInformation'] as const;
function text(form: FormData, name: string) {
  return String(form.get(name) ?? '').trim();
}

/** Sends human-entered source facts through FHIR; clinical codes have no invented defaults. */
export function ClinicianActionsPage({ apiBaseUrl }: ClinicianActionsPageProps) {
  const { t } = useTranslation();
  const [caseRef, setCaseRef] = useState('');
  const [kind, setKind] = useState<(typeof actions)[number]>('clinical-review');
  const [status, setStatus] = useState('');
  const [proposedOwner, setProposedOwner] = useState('');
  const [evidenceChecked, setEvidenceChecked] = useState(false);
  const action = useSimulatorAction<ActionResult>(apiBaseUrl);
  const cases = useSimulatorRead<{ cases: SimulatedCase[] }>(apiBaseUrl, '/cases', true);
  const current = cases.data?.cases.find((item) => item.caseRef === caseRef);
  const statuses =
    kind === 'appointment'
      ? ['booked', 'fulfilled', 'noshow']
      : kind === 'consent'
        ? ['active', 'inactive', 'rejected']
        : kind === 'referral-decision'
          ? ['accepted', 'rejected']
          : kind === 'referral'
            ? ['requested', 'accepted', 'rejected']
            : kind === 'consultation-answer'
              ? ['accepted', 'rejected', 'completed']
              : [];
  return (
    <section>
      <h2>{t('clinicianActions.title')}</h2>
      <p>{t('clinicianActions.description')}</p>
      <p>{t('clinicianActions.codes')}</p>
      <ApiStatus error={cases.error} pending={cases.pending} />
      {cases.data?.cases.length === 0 && <p>{t('clinicianActions.noCases')}</p>}
      {current !== undefined && (
        <p>
          {t('clinicianActions.patientLine', {
            name: current.patient.displayName,
            mrn: current.patient.mrn,
            caseRef: current.caseRef,
          })}
        </p>
      )}
      {typeof current?.approvedHandoffSummary === 'string' && (
        <section data-testid="handoff-summary">
          <h3>{t('clinicianActions.handoffSummary')}</h3>
          <p>{t('clinicianActions.handoffSummaryNote')}</p>
          <pre>{current.approvedHandoffSummary}</pre>
        </section>
      )}
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (kind === 'closure' && !evidenceChecked) return;
          const values = new FormData(event.currentTarget);
          const body: Record<string, unknown> = {
            caseRef,
            performer: {
              syntheticId: text(values, 'performer'),
              role: text(values, 'role'),
              facility: text(values, 'facility') || null,
            },
          };
          if (statuses.length > 0) body.status = status;
          if (kind === 'clinical-review') body.urgency = text(values, 'urgency');
          if (kind === 'outcome') {
            body.evidenceKind = text(values, 'evidenceKind');
            body.asFollowUpReport = values.has('asFollowUpReport');
          }
          if (kind === 'referral-decision') body.accepted = status === 'accepted';
          if (kind === 'consultation-request') body.status = 'requested';
          if (kind === 'consultation-request' || kind === 'referral') {
            const owner = text(values, 'proposedOwner');
            if (owner)
              body.proposedOwner = {
                syntheticId: owner,
                role: text(values, 'proposedRole'),
                facility: text(values, 'receivingFacility') || null,
              };
            if (text(values, 'receivingFacility'))
              body.receivingFacility = text(values, 'receivingFacility');
          }
          if (text(values, 'reason')) body.reason = text(values, 'reason');
          if (kind === 'appointment') {
            if (text(values, 'location')) body.location = text(values, 'location');
            if (text(values, 'scheduledFor'))
              body.scheduledFor = new Date(text(values, 'scheduledFor')).toISOString();
          }
          if (kind === 'consent' && text(values, 'caregiverContact'))
            body.caregiverContact = text(values, 'caregiverContact');
          if (kind === 'closure') {
            body.closureReason = text(values, 'closureReason');
            if (text(values, 'evidenceResourceKey'))
              body.evidenceResourceKey = text(values, 'evidenceResourceKey');
            if (text(values, 'exceptionNote')) body.exceptionNote = text(values, 'exceptionNote');
            if (text(values, 'nextSurveillanceDate'))
              body.nextSurveillanceDate = text(values, 'nextSurveillanceDate');
            if (text(values, 'attempts'))
              body.documentedAttemptReferences = text(values, 'attempts')
                .split(/\r?\n/)
                .filter(Boolean);
          }
          void action.send('/actions/' + kind, body);
        }}
      >
        <fieldset disabled={action.pending}>
          <legend>{t('clinicianActions.sourceActor')}</legend>
          <div className="form-grid">
            <label>
              {t('clinicianActions.case')}
              <select
                name="caseRef"
                required
                value={caseRef}
                onChange={(event) => setCaseRef(event.target.value)}
              >
                <option value="">{t('common.choose')}</option>
                {cases.data?.cases.map((item) => (
                  <option key={item.caseRef} value={item.caseRef}>
                    {t('clinicianActions.caseOption', {
                      name: item.patient.displayName,
                      mrn: item.patient.mrn,
                      caseRef: item.caseRef,
                    })}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('clinicianActions.action')}
              <select
                name="action"
                value={kind}
                onChange={(event) => {
                  const selected = actions.find((item) => item === event.target.value);
                  if (selected !== undefined) {
                    setKind(selected);
                    setStatus('');
                    setProposedOwner('');
                    setEvidenceChecked(false);
                  }
                }}
              >
                {actions.map((item) => (
                  <option key={item} value={item}>
                    {t('clinicianActions.actions.' + item)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('clinicianActions.performer')}
              <input name="performer" required pattern="SYN-.+" />
            </label>
            <label>
              {t('clinicianActions.role')}
              <select name="role" required>
                <option value="">{t('common.choose')}</option>
                {roles.map((role) => (
                  <option key={role} value={role}>
                    {t('clinicianActions.roles.' + role)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('clinicianActions.facility')}
              <input name="facility" required pattern="SYN-.+" />
            </label>
          </div>
          <div key={kind} className="form-grid">
            {statuses.length > 0 && (
              <label>
                {t('clinicianActions.status')}
                <select
                  required
                  name="status"
                  value={status}
                  onChange={(event) => setStatus(event.target.value)}
                >
                  <option value="">{t('common.choose')}</option>
                  {statuses.map((item) => (
                    <option key={item} value={item}>
                      {t('common.status.' + item)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {kind === 'clinical-review' && (
              <label>
                {t('clinicianActions.urgency')}
                <input name="urgency" required />
              </label>
            )}
            {kind === 'outcome' && (
              <>
                <label>
                  {t('clinicianActions.evidenceKind')}
                  <input name="evidenceKind" required />
                </label>
                <label className="checkbox">
                  <input name="asFollowUpReport" type="checkbox" />
                  {t('clinicianActions.followUpReport')}
                </label>
              </>
            )}
            {(kind === 'consultation-request' || kind === 'referral') && (
              <>
                <label>
                  {t('clinicianActions.proposedOwner')}
                  <input
                    name="proposedOwner"
                    pattern="SYN-.+"
                    value={proposedOwner}
                    onChange={(event) => setProposedOwner(event.target.value)}
                  />
                </label>
                <label>
                  {t('clinicianActions.proposedRole')}
                  <select name="proposedRole" required={proposedOwner !== ''}>
                    <option value="">{t('common.choose')}</option>
                    {roles.map((role) => (
                      <option key={role} value={role}>
                        {t('clinicianActions.roles.' + role)}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  {t('clinicianActions.receivingFacility')}
                  <input
                    name="receivingFacility"
                    pattern="SYN-.+"
                    required={proposedOwner !== ''}
                  />
                </label>
              </>
            )}
            {['referral', 'referral-decision', 'consultation-answer'].includes(kind) && (
              <label>
                {t('clinicianActions.reason')}
                <select name="reason" required={status === 'rejected'}>
                  <option value="">{t('common.choose')}</option>
                  {declineReasons.map((reason) => (
                    <option key={reason} value={t('clinicianActions.declineReasons.' + reason)}>
                      {t('clinicianActions.declineReasons.' + reason)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {kind === 'appointment' && (
              <>
                <label>
                  {t('clinicianActions.location')}
                  <input name="location" pattern="SYN.+" />
                </label>
                <label>
                  {t('clinicianActions.scheduledFor')}
                  <input name="scheduledFor" type="datetime-local" />
                </label>
              </>
            )}
            {kind === 'consent' && (
              <label>
                {t('clinicianActions.caregiver')}
                <input name="caregiverContact" required={status === 'active'} pattern="SYN-.+" />
              </label>
            )}
            {kind === 'closure' && (
              <>
                <fieldset>
                  <legend>{t('clinicianActions.closureReason')}</legend>
                  {closureReasons.map((reason) => (
                    <label key={reason} className="checkbox">
                      <input type="radio" name="closureReason" value={reason} required />
                      {t('clinicianActions.closureReasons.' + reason)}
                    </label>
                  ))}
                </fieldset>
                <label>
                  {t('clinicianActions.evidence')}
                  <select name="evidenceResourceKey">
                    <option value="">{t('common.none')}</option>
                    {Object.entries(current?.resourceIds ?? {})
                      .filter(
                        ([, value]) =>
                          value.startsWith('Procedure/') || value.startsWith('DiagnosticReport/'),
                      )
                      .map(([key, value]) => (
                        <option key={key} value={key}>
                          {key} — {value}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  {t('clinicianActions.exception')}
                  <textarea name="exceptionNote" />
                </label>
                <label>
                  {t('clinicianActions.surveillance')}
                  <input name="nextSurveillanceDate" type="date" />
                </label>
                <label>
                  {t('clinicianActions.attempts')}
                  <textarea name="attempts" />
                </label>
                <label className="checkbox">
                  <input
                    name="evidenceChecked"
                    type="checkbox"
                    checked={evidenceChecked}
                    onChange={(event) => setEvidenceChecked(event.target.checked)}
                  />
                  {t('clinicianActions.evidenceChecked')}
                </label>
              </>
            )}
          </div>
          {kind === 'closure' && current?.careLoopTaskId === null && (
            <p>{t('clinicianActions.taskUnknown')}</p>
          )}
          <button
            disabled={!current || cases.error !== null || (kind === 'closure' && !evidenceChecked)}
          >
            {t('clinicianActions.submit')}
          </button>
        </fieldset>
      </form>
      <ApiStatus error={action.error} pending={action.pending} />
      {action.result !== null && (
        <div role="status">
          <h3>{t('clinicianActions.lastResult')}</h3>
          <strong>
            {action.result.accepted
              ? t('clinicianActions.accepted')
              : t('clinicianActions.refused')}
          </strong>
          <ul>
            {action.result.messages.map((message, index) => (
              <li key={index}>
                {message.resourceType}: {message.statusCode}
              </li>
            ))}
          </ul>
          <p>{t('clinicianActions.inspectLog')}</p>
        </div>
      )}
    </section>
  );
}
