import { useTranslation } from 'react-i18next';

import type { InboxMessage } from '../../api';
import { Icon, SimulatedBadge } from '../../components';
import { formatTime } from '../../format';

/** Props for {@link MessageRecords}. */
export interface MessageRecordsProps {
  /** The inbox message whose records to show. */
  message: InboxMessage;
}

/** Props for {@link RecordRow}. */
interface RecordRowProps {
  /** `ok` when CareLoop holds this record, `no` when it failed, `todo` when there is nothing yet. */
  mark: 'ok' | 'no' | 'todo';
  /** The record in a few words. */
  title: string;
  /** What the record means. */
  detail: string;
  /** When it was recorded, already formatted; empty when not recorded. */
  time?: string;
  /** Test id of the title, so tests can read the state. */
  testId: string;
}

/** One line of the record list: a mark, a title with its meaning, and the time. */
function RecordRow({ mark, title, detail, time, testId }: RecordRowProps) {
  return (
    <li>
      <span className={`mark mark-${mark}`}>
        {mark === 'ok' && <Icon name="check" />}
        {mark === 'no' && <Icon name="x" />}
      </span>
      <div>
        <b data-testid={testId}>{title}</b>
        <span>{detail}</span>
      </div>
      <span className="record-time">{time}</span>
    </li>
  );
}

/**
 * What CareLoop has been told about one message: its delivery, the patient's response and the
 * patient's confidence rating.
 *
 * @remarks
 * The three are kept apart on purpose. Delivery is a transport fact and never an
 * acknowledgement; a call request and a rating are not acknowledgements either. A caregiver's
 * copy shows delivery only, because nothing a caregiver does is recorded as the patient's.
 */
export function MessageRecords({ message }: MessageRecordsProps) {
  const { t } = useTranslation();
  const isCaregiverCopy = message.recipientKind === 'Caregiver';
  const deliveryMark =
    message.delivery === 'Delivered' ? 'ok' : message.delivery === 'Failed' ? 'no' : 'todo';

  return (
    <aside className="card records" aria-labelledby={`records-${message.messageId}`}>
      <h3 id={`records-${message.messageId}`}>
        {t('inbox.records.heading')}
        <SimulatedBadge />
      </h3>
      <ul className="check">
        <RecordRow
          mark={deliveryMark}
          title={t(`inbox.records.delivery.${message.delivery}`)}
          detail={t(`inbox.records.deliveryDetail.${message.delivery}`, {
            reason: message.deliveryFailureReason ?? '',
          })}
          time={deliveryMark === 'todo' ? '' : formatTime(message.receivedAt)}
          testId="delivery-state"
        />
        {isCaregiverCopy ? (
          <RecordRow
            mark="todo"
            title={t('inbox.records.caregiver')}
            detail={t('inbox.records.caregiverDetail')}
            testId="caregiver-note"
          />
        ) : (
          <>
            <RecordRow
              mark={message.response === 'None' ? 'todo' : 'ok'}
              title={t(`inbox.records.response.${message.response}`)}
              detail={t(`inbox.records.responseDetail.${message.response}`)}
              time={formatTime(message.respondedAt)}
              testId="response-state"
            />
            <RecordRow
              mark={message.confidence === null ? 'todo' : 'ok'}
              title={
                message.confidence === null
                  ? t('inbox.records.confidenceNone')
                  : t('inbox.records.confidence', { rating: message.confidence })
              }
              detail={t('inbox.records.confidenceDetail')}
              time={formatTime(message.confidenceRatedAt)}
              testId="confidence-state"
            />
          </>
        )}
      </ul>
    </aside>
  );
}
