import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { listInbox } from '../../api';
import type { InboxChannel, InboxMessage } from '../../api';
import { formatTime } from '../../format';
import { useLoad } from '../../hooks';

import { BasicPhoneView } from './BasicPhoneView';
import { EmailView } from './EmailView';
import { MessageRecords } from './MessageRecords';
import { SmartphoneView } from './SmartphoneView';

/** The inboxes, in the order their tabs are shown. */
const CHANNELS: readonly InboxChannel[] = ['App', 'Portal', 'Sms', 'Email'];

/** Props for {@link InboxPage}. */
export interface InboxPageProps {
  /** How often to re-read the inbox, in milliseconds. Polling is off when omitted. */
  pollIntervalMs?: number;
}

/**
 * The simulated patient inbox: what CareLoop sent, shown the way each channel shows it.
 *
 * @remarks
 * One tab per channel. The health app and the patient portal show a smartphone, a text message
 * shows a basic phone with a keypad, and a caregiver's copy shows a read-only email. Beside the
 * device is what CareLoop has recorded for that message. A message being delivered is not an
 * acknowledgement: only the patient's explicit action (or the patient's behaviour) reports a
 * response. Until a tab is chosen the first inbox that has a message is shown, and until a
 * message is chosen the newest one in that inbox is.
 */
export function InboxPage({ pollIntervalMs }: InboxPageProps) {
  const { t } = useTranslation();
  const { data, error, loading, setData } = useLoad(listInbox, pollIntervalMs);
  const [chosenChannel, setChosenChannel] = useState<InboxChannel | null>(null);
  const [chosenId, setChosenId] = useState<string | null>(null);

  const messages = data ?? [];
  const channel =
    chosenChannel ?? CHANNELS.find((c) => messages.some((m) => m.channel === c)) ?? 'App';
  const inChannel = messages.filter((message) => message.channel === channel);
  const selected = inChannel.find((message) => message.messageId === chosenId) ?? inChannel[0];

  const replace = useCallback(
    (updated: InboxMessage) =>
      setData((current) => current?.map((m) => (m.messageId === updated.messageId ? updated : m))),
    [setData],
  );

  return (
    <section aria-labelledby="inbox-title">
      <h2 id="inbox-title">{t('inbox.title')}</h2>
      <p className="lead">{t('inbox.description')}</p>

      <div className="chips" role="tablist" aria-label={t('inbox.channelsLabel')}>
        {CHANNELS.map((option) => (
          <button
            key={option}
            type="button"
            role="tab"
            className="chip"
            aria-selected={option === channel}
            onClick={() => setChosenChannel(option)}
          >
            {t('inbox.channelTab', {
              channel: t(`inbox.channel.${option}`),
              count: messages.filter((message) => message.channel === option).length,
            })}
          </button>
        ))}
      </div>

      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {loading && <p>{t('common.loading')}</p>}
      {!loading && !error && inChannel.length === 0 && <p className="empty">{t('inbox.empty')}</p>}

      {selected && (
        <div className="inbox-layout">
          <ul className="message-picker" aria-label={t('inbox.messagesLabel')}>
            {inChannel.map((message) => (
              <li key={message.messageId}>
                <button
                  type="button"
                  aria-pressed={message.messageId === selected.messageId}
                  aria-label={t('inbox.messageItem', {
                    name: message.patientDisplayName,
                    time: formatTime(message.receivedAt),
                  })}
                  onClick={() => setChosenId(message.messageId)}
                >
                  <b>{message.patientDisplayName}</b>
                  <small>{formatTime(message.receivedAt)}</small>
                </button>
              </li>
            ))}
          </ul>

          {channel === 'Sms' && (
            <BasicPhoneView key={selected.messageId} message={selected} onChanged={replace} />
          )}
          {channel === 'Email' && <EmailView message={selected} />}
          {(channel === 'App' || channel === 'Portal') && (
            <SmartphoneView key={selected.messageId} message={selected} onChanged={replace} />
          )}

          <MessageRecords message={selected} />
        </div>
      )}
    </section>
  );
}
