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
 * Says which explanation a reply gets.
 *
 * @param text - The reply as typed.
 * @returns `WillCome` for 1, `CallMe` for 2 and `Other` for anything else, ignoring surrounding spaces.
 */
function meaningOf(text: string): 'WillCome' | 'CallMe' | 'Other' {
  const trimmed = text.trim();
  return trimmed === '1' ? 'WillCome' : trimmed === '2' ? 'CallMe' : 'Other';
}

/**
 * What CareLoop has been told about one message: its delivery, and each reply the patient sent.
 *
 * @remarks
 * The two are kept apart on purpose. Delivery is a transport fact and never an acknowledgement.
 * A reply is shown as typed, with how CareLoop reads it: 1 is "I got this message", and 2 or any other
 * text asks for a call and is never an acknowledgement. Whether the patient understood, and how
 * confident they feel, are not shown here: a nurse records those in CareLoop after a call. A
 * caregiver's or a staff member's message shows delivery only, because only the patient replies.
 */
export function MessageRecords({ message }: MessageRecordsProps) {
  const { t } = useTranslation();
  const forPatient = message.recipientKind === 'Patient';
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
        {!forPatient && (
          <RecordRow
            mark="todo"
            title={t('inbox.records.onlyPatient')}
            detail={t(`inbox.records.onlyPatientDetail.${message.recipientKind}`)}
            testId="read-only-note"
          />
        )}
        {forPatient && message.replies.length === 0 && (
          <RecordRow
            mark="todo"
            title={t('inbox.records.noReply')}
            detail={t('inbox.records.noReplyDetail')}
            testId="reply-state"
          />
        )}
        {forPatient &&
          message.replies.map((reply) => (
            <RecordRow
              key={reply.replyId}
              mark="ok"
              title={t('inbox.records.replied', { text: reply.text })}
              detail={t(`inbox.records.meaning.${meaningOf(reply.text)}`)}
              time={formatTime(reply.sentAt)}
              testId="reply-state"
            />
          ))}
        {forPatient && (
          <RecordRow
            mark="todo"
            title={t('inbox.records.byNurse')}
            detail={t('inbox.records.byNurseDetail')}
            testId="nurse-note"
          />
        )}
      </ul>
    </aside>
  );
}
