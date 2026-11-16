import { useTranslation } from 'react-i18next';

import type { InboxMessage } from '../../api';
import { formatTime } from '../../format';

/** Props for {@link EmailView}. */
export interface EmailViewProps {
  /** The inbox message to show; a caregiver's copy in the email inbox. */
  message: InboxMessage;
}

/**
 * A caregiver's copy, shown as the email the caregiver would receive.
 *
 * @remarks
 * Read-only by design. A patient may name someone to receive the day, time and place of their
 * appointments, but only the patient replies to the clinic. The email therefore has no button,
 * no reply box and no link, and nothing shown here can send an event to CareLoop.
 */
export function EmailView({ message }: EmailViewProps) {
  const { t } = useTranslation();

  return (
    <div className="device-column device-column-wide">
      <div className="mail" role="group" aria-label={t('inbox.channel.Email')}>
        <dl className="mail-head">
          <div>
            <dt>{t('inbox.email.from')}</dt>
            <dd>{t('inbox.email.fromValue')}</dd>
          </div>
          <div>
            <dt>{t('inbox.email.to')}</dt>
            <dd>{message.patientDisplayName}</dd>
          </div>
          <div>
            <dt>{t('inbox.email.sent')}</dt>
            <dd>{formatTime(message.receivedAt)}</dd>
          </div>
        </dl>
        <p className="mail-body message-body" lang={message.language}>
          {message.body}
        </p>
      </div>
      <p className="device-caption device-caption-start">{t('inbox.email.readOnly')}</p>
    </div>
  );
}
