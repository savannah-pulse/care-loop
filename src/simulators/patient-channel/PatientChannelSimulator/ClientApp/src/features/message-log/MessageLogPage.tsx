import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { listFhirLog } from '../../api';
import { SimulatedBadge } from '../../components';
import { formatJson, formatTime } from '../../format';
import { useLoad } from '../../hooks';

/** Props for {@link MessageLogPage}. */
export interface MessageLogPageProps {
  /** Maximum number of log entries to show. All entries are shown when omitted. */
  maxEntries?: number;
}

/**
 * The log of every FHIR exchange between this simulator and CareLoop.
 *
 * @remarks
 * Exists so a demo audience can see exactly what crossed the boundary: each notification request
 * CareLoop sent, each delivery report and patient response sent back, and each case-status read.
 * Selecting a row shows its FHIR JSON. The log is read when the screen opens and on "Refresh".
 */
export function MessageLogPage({ maxEntries }: MessageLogPageProps) {
  const { t } = useTranslation();
  const { data, error, loading, reload } = useLoad(listFhirLog);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const entries = maxEntries === undefined ? (data ?? []) : (data ?? []).slice(0, maxEntries);
  const selected = entries.find((entry) => entry.entryId === selectedId);

  return (
    <section aria-labelledby="log-title">
      <h2 id="log-title">{t('messageLog.title')}</h2>
      <p>{t('messageLog.description')}</p>
      <button type="button" onClick={reload}>
        {t('common.refresh')}
      </button>

      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {loading && <p>{t('common.loading')}</p>}
      {!loading && !error && entries.length === 0 && <p>{t('messageLog.empty')}</p>}

      {entries.length > 0 && (
        <table>
          <thead>
            <tr>
              <th scope="col">{t('messageLog.columns.at')}</th>
              <th scope="col">{t('messageLog.columns.direction')}</th>
              <th scope="col">{t('messageLog.columns.request')}</th>
              <th scope="col">{t('messageLog.columns.resource')}</th>
              <th scope="col">{t('messageLog.columns.status')}</th>
              <th scope="col">{t('messageLog.columns.marker')}</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.entryId} aria-selected={entry.entryId === selectedId}>
                <td>{formatTime(entry.at)}</td>
                <td>{t(`messageLog.direction.${entry.direction}`)}</td>
                <td>
                  <button
                    type="button"
                    className="link-button"
                    aria-label={t('messageLog.show', {
                      resource: entry.resourceType,
                      method: entry.method,
                      time: formatTime(entry.at),
                    })}
                    onClick={() => setSelectedId(entry.entryId)}
                  >
                    {entry.method} {entry.url}
                  </button>
                </td>
                <td>
                  {entry.resourceType}
                  {entry.resourceId ? `/${entry.resourceId}` : ''}
                </td>
                <td>{entry.statusCode ?? t('messageLog.noResponse')}</td>
                <td>{entry.simulated && <SimulatedBadge />}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {entries.length > 0 && (
        <article className="panel" aria-labelledby="log-detail-title">
          <h3 id="log-detail-title">{t('messageLog.detailHeading')}</h3>
          {!selected && <p>{t('messageLog.selectHint')}</p>}
          {selected && !selected.body && <p>{t('messageLog.noBody')}</p>}
          {selected?.body && (
            <pre className="fhir-json" data-testid="fhir-json">
              {formatJson(selected.body)}
            </pre>
          )}
        </article>
      )}
    </section>
  );
}
