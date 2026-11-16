import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { replyToMessage } from '../../api';
import type { InboxMessage } from '../../api';
import { formatTime } from '../../format';

import { useMessageAction } from './useMessageAction';

/** Props for {@link EmailView}. */
export interface EmailViewProps {
  /** The email to show. */
  message: InboxMessage;
  /** Called with the updated message after CareLoop accepted a reply. */
  onChanged: (message: InboxMessage) => void;
}

/**
 * An email, shown as the mailbox of whoever received it shows it.
 *
 * @remarks
 * The email carries a subject and the text exactly as CareLoop worded it, and no link. A patient
 * replies in the box under it, the same way as to a text message: the reply goes to CareLoop as
 * typed, and CareLoop reads 1 as "I will come" and anything else as a request for a call. A
 * caregiver's or a staff member's email is read-only by design: a patient may name someone to
 * receive the day, time and place of their appointments, but only the patient replies to the
 * clinic, so those emails have no reply box and nothing shown can send an event to CareLoop.
 */
export function EmailView({ message, onChanged }: EmailViewProps) {
  const { t } = useTranslation();
  const { busy, error, send } = useMessageAction(onChanged);
  const [draft, setDraft] = useState('');
  const replyId = useId();

  const forPatient = message.recipientKind === 'Patient';
  const reachedPatient = message.delivery === 'Delivered' || message.delivery === 'Silent';

  const reply = async () => {
    if (await send(() => replyToMessage(message.messageId, draft))) {
      setDraft('');
    }
  };

  return (
    <div className="device-column device-column-wide">
      <div className="mail" role="group" aria-label={t('inbox.email.deviceLabel')}>
        <dl className="mail-head">
          <div>
            <dt>{t('inbox.email.from')}</dt>
            <dd>{t('inbox.email.fromValue')}</dd>
          </div>
          <div>
            <dt>{t('inbox.email.to')}</dt>
            <dd>
              {message.displayName} ({message.to})
            </dd>
          </div>
          <div>
            <dt>{t('inbox.email.subject')}</dt>
            <dd data-testid="email-subject">{message.subject ?? t('inbox.email.noSubject')}</dd>
          </div>
          <div>
            <dt>{t('inbox.email.sent')}</dt>
            <dd>{formatTime(message.receivedAt)}</dd>
          </div>
        </dl>
        <p className="mail-body message-body" lang={message.language}>
          {message.body}
        </p>

        {forPatient && (
          <form
            className="mail-reply"
            onSubmit={(event) => {
              event.preventDefault();
              void reply();
            }}
          >
            <label htmlFor={replyId}>{t('inbox.email.replyLabel')}</label>
            <textarea
              id={replyId}
              rows={3}
              value={draft}
              disabled={busy || !reachedPatient}
              onChange={(event) => setDraft(event.target.value)}
            />
            <button type="submit" disabled={busy || !reachedPatient || draft.trim().length === 0}>
              {t('inbox.email.send')}
            </button>
          </form>
        )}
      </div>

      {forPatient && !reachedPatient && (
        <p className="device-caption device-caption-start">{t('inbox.notAnswerable')}</p>
      )}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <p className="device-caption device-caption-start">
        {forPatient ? t('inbox.email.caption') : t(`inbox.readOnly.${message.recipientKind}`)}
      </p>
    </div>
  );
}
