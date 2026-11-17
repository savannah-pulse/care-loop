import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { replyToMessage } from '../../api';
import type { InboxMessage } from '../../api';
import { formatClock } from '../../format';

import { useMessageAction } from './useMessageAction';

/** The keys of a basic phone, in keypad order. */
const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'] as const;

/** Props for {@link BasicPhoneView}. */
export interface BasicPhoneViewProps {
  /** The text message to show. */
  message: InboxMessage;
  /** Called with the updated message after CareLoop accepted the reply. */
  onChanged: (message: InboxMessage) => void;
}

/**
 * A text message as someone with a basic phone and no internet sees it.
 *
 * @remarks
 * The screen shows the text exactly as CareLoop worded it. The patient types a reply on the
 * keypad and presses "Reply". The reply goes to CareLoop as typed: CareLoop reads 1 as "I will
 * come" (an acknowledgement) and anything else, 2 included, as a request for a call, so a
 * mistyped key is never taken for an acknowledgement. The keypad is disabled when the text never
 * reached the patient, and on a caregiver's or a staff member's message, which nobody replies to.
 */
export function BasicPhoneView({ message, onChanged }: BasicPhoneViewProps) {
  const { t } = useTranslation();
  const { busy, error, send } = useMessageAction(onChanged);
  const [draft, setDraft] = useState('');

  const forPatient = message.recipientKind === 'Patient';
  const reachedPatient = message.delivery === 'Delivered' || message.delivery === 'Silent';
  const locked = busy || !reachedPatient || !forPatient;

  const reply = async () => {
    await send(() => replyToMessage(message.messageId, draft));
    setDraft('');
  };

  return (
    <div className="device-column">
      <div className="basic-phone" role="group" aria-label={t('inbox.sms.deviceLabel')}>
        <div className="basic-speaker" />
        <div className="basic-screen">
          <div className="basic-head">
            <span>{t('inbox.sms.from')}</span>
            <span>{formatClock(message.receivedAt)}</span>
          </div>
          <p className="message-body" lang={message.language}>
            {message.body}
          </p>
          <p className="basic-draft" aria-live="polite" data-testid="sms-draft">
            {draft && t('inbox.sms.draft', { text: draft })}
          </p>
        </div>
        <div className="basic-soft">
          <button
            type="button"
            disabled={locked || draft.length === 0}
            onClick={() => void reply()}
          >
            {t('inbox.sms.reply')}
          </button>
          <button
            type="button"
            disabled={locked || draft.length === 0}
            onClick={() => setDraft('')}
          >
            {t('inbox.sms.back')}
          </button>
        </div>
        <div className="basic-keys" role="group" aria-label={t('inbox.sms.keypadLabel')}>
          {KEYS.map((key) => (
            <button
              key={key}
              type="button"
              disabled={locked}
              aria-label={t('inbox.sms.key', { key })}
              onClick={() => setDraft((current) => (current + key).slice(0, 12))}
            >
              {key}
            </button>
          ))}
        </div>
      </div>

      {forPatient && !reachedPatient && (
        <p className="device-caption">{t('inbox.notAnswerable')}</p>
      )}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <p className="device-caption">
        {forPatient ? t('inbox.sms.caption') : t(`inbox.readOnly.${message.recipientKind}`)}
      </p>
    </div>
  );
}
