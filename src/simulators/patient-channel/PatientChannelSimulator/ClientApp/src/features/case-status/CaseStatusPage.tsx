import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';

import { ApiError, getCaseStatus } from '../../api';
import type { PatientCaseStatus } from '../../api';
import { SimulatedBadge } from '../../components';
import { formatTime } from '../../format';
import { errorMessage } from '../../hooks';

/** Props for {@link CaseStatusPage}. */
export interface CaseStatusPageProps {
  /** Logical id of the case `Task` to show. When omitted the `taskId` query parameter is used, and otherwise the screen asks for one. */
  taskId?: string;
}

/**
 * The patient's view of a case, read from the CareLoop case `Task`.
 *
 * @remarks
 * Shows coordination status only (stage, status, deadline, last update). It never shows a result,
 * diagnosis, urgency or who owns the case: the patient channel is not a clinical system. Looking
 * a case up makes one request to the simulator's host, which reads the `Task` from CareLoop.
 */
export function CaseStatusPage({ taskId }: CaseStatusPageProps) {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const initial = taskId ?? params.get('taskId') ?? '';
  const [input, setInput] = useState(initial);
  const [wanted, setWanted] = useState(initial);
  const [status, setStatus] = useState<PatientCaseStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!wanted) {
      return undefined;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    getCaseStatus(wanted)
      .then((value) => {
        if (!cancelled) {
          setStatus(value);
        }
      })
      .catch((failure: unknown) => {
        if (!cancelled) {
          setStatus(null);
          setError(
            failure instanceof ApiError && failure.status === 404
              ? t('caseStatus.notFound')
              : errorMessage(failure),
          );
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [wanted, t]);

  const lookUp = (event: FormEvent) => {
    event.preventDefault();
    setWanted(input.trim());
  };

  const fields: readonly (readonly [string, string | null])[] = status
    ? [
        ['taskId', status.taskId],
        ['stage', status.stage],
        ['taskStatus', status.taskStatus],
        ['dueBy', status.dueBy && formatTime(status.dueBy)],
        ['lastUpdated', status.lastUpdated && formatTime(status.lastUpdated)],
        ['statusReason', status.statusReason],
      ]
    : [];

  return (
    <section aria-labelledby="case-status-title">
      <h2 id="case-status-title">{t('caseStatus.title')}</h2>
      <p>{t('caseStatus.description')}</p>

      <form className="inline-form" onSubmit={lookUp}>
        <label htmlFor="case-task-id">{t('caseStatus.taskIdLabel')}</label>
        <input
          id="case-task-id"
          value={input}
          onChange={(event) => setInput(event.target.value)}
          autoComplete="off"
        />
        <button type="submit" disabled={loading || input.trim().length === 0}>
          {t('caseStatus.lookUp')}
        </button>
      </form>

      {loading && <p>{t('common.loading')}</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}

      {status && !loading && (
        <article className="panel" aria-label={t('caseStatus.title')}>
          <SimulatedBadge />
          <dl className="field-list">
            {fields.map(([key, value]) => (
              <div key={key}>
                <dt>{t(`caseStatus.fields.${key}`)}</dt>
                <dd data-testid={`case-${key}`}>{value || t('caseStatus.notStated')}</dd>
              </div>
            ))}
          </dl>
        </article>
      )}
    </section>
  );
}
