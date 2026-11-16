import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { LogEntry } from '../../api/simulator';
import { useSimulatorRead } from '../../api/useSimulatorRead';
import { ApiStatus } from '../../components/ApiStatus';

/** Props for viewing exact FHIR exchanges. */
export interface FhirLogPageProps {
  /** Optional simulator API base URL; omission uses the current origin. */
  apiBaseUrl?: string;
}
/** How many of the newest log entries the polled list asks for; the server keeps a bounded log too. */
const listLimit = 200;
/** Polls sent/received wire logs and makes refused responses inspectable without changing case state. */
export function FhirLogPage({ apiBaseUrl }: FhirLogPageProps) {
  const { t } = useTranslation();
  const [direction, setDirection] = useState('');
  const [caseRef, setCaseRef] = useState('');
  const [selected, setSelected] = useState<string | null>(null);
  const params = new URLSearchParams();
  if (direction) params.set('direction', direction);
  if (caseRef) params.set('caseRef', caseRef);
  params.set('limit', String(listLimit));
  const list = useSimulatorRead<{ entries: LogEntry[] }>(
    apiBaseUrl,
    '/fhir-log?' + params.toString(),
    true,
  );
  const detail = list.data?.entries.find((entry) => entry.id === selected);
  return (
    <section>
      <h2>{t('fhirLog.title')}</h2>
      <p>{t('fhirLog.description')}</p>
      <div className="form-grid">
        <label>
          {t('fhirLog.direction')}
          <select
            name="direction"
            value={direction}
            onChange={(event) => {
              setDirection(event.target.value);
              setSelected(null);
            }}
          >
            <option value="">{t('common.all')}</option>
            <option value="sent">{t('common.status.sent')}</option>
            <option value="received">{t('common.status.received')}</option>
          </select>
        </label>
        <label>
          {t('fhirLog.caseRef')}
          <input
            name="caseRef"
            value={caseRef}
            onChange={(event) => {
              setCaseRef(event.target.value);
              setSelected(null);
            }}
          />
        </label>
      </div>
      <ApiStatus error={list.error} pending={list.pending} />
      {list.data?.entries.length === 0 && <p>{t('common.empty')}</p>}
      <ul className="cards">
        {list.data?.entries.map((entry) => (
          <li key={entry.id}>
            <strong>
              {entry.method} {entry.resourceType}
            </strong>{' '}
            — {t('common.status.' + entry.direction)} —{' '}
            {entry.statusCode ?? t('fhirLog.noResponse')}
            <p>{entry.caseRef}</p>
            <time>{entry.at}</time>
            <button onClick={() => setSelected(entry.id)}>{t('common.open')}</button>
          </li>
        ))}
      </ul>
      {detail !== undefined && (
        <aside>
          <h3>{t('fhirLog.requestBody')}</h3>
          <pre>{detail.body}</pre>
          <h3>{t('fhirLog.responseBody')}</h3>
          <pre>{detail.responseBody ?? t('fhirLog.noResponse')}</pre>
          <button onClick={() => setSelected(null)}>{t('common.close')}</button>
        </aside>
      )}
    </section>
  );
}
