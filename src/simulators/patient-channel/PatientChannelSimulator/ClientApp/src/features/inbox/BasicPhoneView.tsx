import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { replyBySms } from '../../api';
import type { InboxMessage } from '../../api';
import { formatClock } from '../../format';

import { useMessageAction } from './useMessageAction';

/** The keys of a basic phone, in keypad order. */
const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'] as const;

/** Props for {@link BasicPhoneView}. */
export interface BasicPhoneViewProps {
  /** The inbox message to show; its channel is the text message inbox. */
  message: InboxMessage;
  /** Called with the updated message after CareLoop accepted the reply. */
  onChanged: (message: InboxMessage) => void;
}

/**
 * A text message as a patient with a basic phone and no internet sees it.
 *
 * @remarks
 * The screen shows the text exactly as CareLoop worded it. The patient types a reply on the
 * keypad and presses "Reply": 1 says they will come (an acknowledgement) and 2 asks for a call.
 * Any other reply is refused by the simulator and nothing is sent to CareLoop, so a mistyped
 * key can never be taken for an acknowledgement. When the text never reached the patient the
 * keypad is disabled.
 */
export function BasicPhoneView({ message, onChanged }: BasicPhoneViewProps) {
  const { t } = useTranslation();
  const { busy, error, send } = useMessageAction(onChanged);
  const [draft, setDraft] = useState('');

  const reachedPatient = message.delivery === 'Delivered' || message.delivery === 'Silent';
  const locked = busy || !reachedPatient;

  const reply = async () => {
    await send(() => replyBySms(message.messageId, draft));
    setDraft('');
  };

  return (
    <div className="device-column">
      <div className="basic-phone" role="group" aria-label={t('inbox.channel.Sms')}>
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

      {!reachedPatient && <p className="device-caption">{t('inbox.notAnswerable')}</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <p className="device-caption">{t('inbox.sms.caption')}</p>
    </div>
  );
}
