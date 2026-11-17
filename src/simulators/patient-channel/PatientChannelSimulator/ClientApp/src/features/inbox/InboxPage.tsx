import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { listInbox } from '../../api';
import type { InboxMessage } from '../../api';
import { formatTime } from '../../format';
import { useLoad } from '../../hooks';

import { BasicPhoneView } from './BasicPhoneView';
import { EmailView } from './EmailView';
import { MessageRecords } from './MessageRecords';

/** One inbox tab: whose messages, and for the patient, on which channel. */
export type InboxKey = 'PatientSms' | 'PatientEmail' | 'Caregiver' | 'Staff';

/** The inboxes, in the order their tabs are shown. */
const INBOXES: readonly InboxKey[] = ['PatientSms', 'PatientEmail', 'Caregiver', 'Staff'];

/**
 * Says which inbox tab a message belongs in.
 *
 * @param message - The message.
 * @returns The patient's text messages or emails, the caregiver inbox, or the staff inbox.
 */
export function inboxOf(message: InboxMessage): InboxKey {
  if (message.recipientKind === 'Caregiver') {
    return 'Caregiver';
  }

  if (message.recipientKind === 'Staff') {
    return 'Staff';
  }

  return message.channel === 'Sms' ? 'PatientSms' : 'PatientEmail';
}

/** Props for {@link InboxPage}. */
export interface InboxPageProps {
  /** How often to re-read the inbox, in milliseconds. Polling is off when omitted. */
  pollIntervalMs?: number;
}

/**
 * The simulated inboxes: every text message and email CareLoop sent.
 *
 * @remarks
 * CareLoop reaches people by text message and email only, so there are two ways a message is
 * shown: a text message on a basic phone with a keypad, and an email in a mailbox. The tabs
 * separate the patient's text messages, the patient's emails, a caregiver's messages and staff
 * notices. Only a message to the patient can be replied to. Beside the message is what CareLoop
 * has been told about it. A message being delivered is not an acknowledgement: only the patient's
 * own reply (typed here, or sent by the patient's behaviour) is reported. Until a tab is chosen
 * the first inbox that has a message is shown, and until a message is chosen the newest one in
 * that inbox is.
 */
export function InboxPage({ pollIntervalMs }: InboxPageProps) {
  const { t } = useTranslation();
  const { data, error, loading, setData } = useLoad(listInbox, pollIntervalMs);
  const [chosenInbox, setChosenInbox] = useState<InboxKey | null>(null);
  const [chosenId, setChosenId] = useState<string | null>(null);

  const messages = data ?? [];
  const inbox =
    chosenInbox ?? INBOXES.find((key) => messages.some((m) => inboxOf(m) === key)) ?? 'PatientSms';
  const inInbox = messages.filter((message) => inboxOf(message) === inbox);
  const selected = inInbox.find((message) => message.messageId === chosenId) ?? inInbox[0];

  const replace = useCallback(
    (updated: InboxMessage) =>
      setData((current) => current?.map((m) => (m.messageId === updated.messageId ? updated : m))),
    [setData],
  );

  return (
    <section aria-labelledby="inbox-title">
      <h2 id="inbox-title">{t('inbox.title')}</h2>
      <p className="lead">{t('inbox.description')}</p>

      <div className="chips" role="tablist" aria-label={t('inbox.inboxesLabel')}>
        {INBOXES.map((option) => (
          <button
            key={option}
            type="button"
            role="tab"
            className="chip"
            aria-selected={option === inbox}
            onClick={() => setChosenInbox(option)}
          >
            {t('inbox.inboxTab', {
              inbox: t(`inbox.inbox.${option}`),
              count: messages.filter((message) => inboxOf(message) === option).length,
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
      {!loading && !error && inInbox.length === 0 && <p className="empty">{t('inbox.empty')}</p>}

      {selected && (
        <div className="inbox-layout">
          <ul className="message-picker" aria-label={t('inbox.messagesLabel')}>
            {inInbox.map((message) => (
              <li key={message.messageId}>
                <button
                  type="button"
                  aria-pressed={message.messageId === selected.messageId}
                  aria-label={t('inbox.messageItem', {
                    name: message.displayName,
                    channel: t(`inbox.channel.${message.channel}`),
                    time: formatTime(message.receivedAt),
                  })}
                  onClick={() => setChosenId(message.messageId)}
                >
                  <b>{message.displayName}</b>
                  <small>
                    {t(`inbox.channel.${message.channel}`)} · {formatTime(message.receivedAt)}
                  </small>
                </button>
              </li>
            ))}
          </ul>

          {selected.channel === 'Sms' ? (
            <BasicPhoneView key={selected.messageId} message={selected} onChanged={replace} />
          ) : (
            <EmailView key={selected.messageId} message={selected} onChanged={replace} />
          )}

          <MessageRecords message={selected} />
        </div>
      )}
    </section>
  );
}
