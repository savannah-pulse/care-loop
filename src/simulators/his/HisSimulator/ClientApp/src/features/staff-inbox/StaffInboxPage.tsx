import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { InboxItem, LogEntry } from '../../api/simulator';
import { useSimulatorRead } from '../../api/useSimulatorRead';
import { ApiStatus } from '../../components/ApiStatus';

/** Props for the receiving hospital's staff inbox. */
export interface StaffInboxPageProps {
  /** Optional simulator API base URL; omission uses the current origin. */
  apiBaseUrl?: string;
}
/** Polls addressed staff notices; opening a message never acknowledges or accepts a handoff. */
export function StaffInboxPage({ apiBaseUrl }: StaffInboxPageProps) {
  const { t } = useTranslation();
  const [recipient, setRecipient] = useState('');
  const [selected, setSelected] = useState<string | null>(null);
  const list = useSimulatorRead<{ items: InboxItem[] }>(
    apiBaseUrl,
    '/staff-inbox' + (recipient ? '?recipient=' + encodeURIComponent(recipient) : ''),
    true,
  );
  const detail = useSimulatorRead<InboxItem>(
    apiBaseUrl,
    selected === null ? null : '/staff-inbox/' + selected,
    selected !== null,
  );
  const logId = detail.data?.logEntryId ?? null;
  const log = useSimulatorRead<LogEntry>(apiBaseUrl, logId === null ? null : '/fhir-log/' + logId);
  const body = log.data?.body;
  return (
    <section>
      <h2>{t('staffInbox.title')}</h2>
      <p>{t('staffInbox.description')}</p>
      <label>
        {t('staffInbox.recipient')}
        <input
          name="recipient"
          value={recipient}
          onChange={(event) => {
            setRecipient(event.target.value);
            setSelected(null);
          }}
        />
      </label>
      <ApiStatus error={list.error} pending={list.pending} />
      {list.data?.items.length === 0 && <p>{t('common.empty')}</p>}
      <ul className="cards">
        {list.data?.items.map((item) => (
          <li key={item.id}>
            <strong>{t('common.kind.' + item.kind)}</strong> — {item.recipientSyntheticId}
            <p>{item.subject}</p>
            <time>{item.receivedAt}</time>
            <button onClick={() => setSelected(item.id)}>{t('common.open')}</button>
          </li>
        ))}
      </ul>
      {selected !== null && (
        <aside>
          <ApiStatus error={detail.error} pending={detail.pending} />
          <ApiStatus error={log.error} pending={log.pending} />
          <h3>{detail.data?.subject}</h3>
          {body !== undefined && <pre>{body}</pre>}
          <button onClick={() => setSelected(null)}>{t('common.close')}</button>
        </aside>
      )}
    </section>
  );
}
