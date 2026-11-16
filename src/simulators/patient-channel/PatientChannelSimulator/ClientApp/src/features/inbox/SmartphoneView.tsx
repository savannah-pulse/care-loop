import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import { actOnInboxMessage, rateConfidence } from '../../api';
import type { InboxMessage } from '../../api';
import { Icon } from '../../components';
import { formatClock } from '../../format';
import { ROUTES } from '../../routes';

import { useMessageAction } from './useMessageAction';

/** The ratings a patient can give, lowest first. */
const RATINGS = [1, 2, 3, 4, 5] as const;

/**
 * The answers to "What will you do next?".
 *
 * @remarks
 * `understand` says the next step back and is sent as teach-back. `wait` and `unsure` both show
 * the patient is not clear about the next step, so each asks the care team for a call.
 */
const ANSWERS = ['understand', 'wait', 'unsure'] as const;

/** One answer to the teach-back question. */
type Answer = (typeof ANSWERS)[number];

/** Props for {@link SmartphoneView}. */
export interface SmartphoneViewProps {
  /** The inbox message to show; its channel is the health app or the patient portal. */
  message: InboxMessage;
  /** Called with the updated message after CareLoop accepted a patient action. */
  onChanged: (message: InboxMessage) => void;
}

/**
 * A message as a patient with a smartphone sees it, inside the health app they already use.
 *
 * @remarks
 * The home screen shows the one next step, exactly as CareLoop worded it, with "I will come"
 * (an acknowledgement and nothing else), "I need another time" and "Call me back" (both ask for
 * a call). Once the patient has responded, the follow-up screen thanks them and asks them to say
 * the next step back and to rate how sure they feel; "Send" reports the answer as teach-back (or
 * as a call request when the patient is unsure) and the rating as a confidence rating. When the
 * message never reached the patient there is nothing to respond to, so no action is offered.
 */
export function SmartphoneView({ message, onChanged }: SmartphoneViewProps) {
  const { t } = useTranslation();
  const { busy, error, send } = useMessageAction(onChanged);
  const [chosenScreen, setChosenScreen] = useState<'home' | 'followUp' | null>(null);
  const [answer, setAnswer] = useState<Answer | null>(null);
  const [rating, setRating] = useState<number | null>(null);

  const id = message.messageId;
  const reachedPatient = message.delivery === 'Delivered' || message.delivery === 'Silent';
  const screen = chosenScreen ?? (message.response === 'None' ? 'home' : 'followUp');
  const shownRating = rating ?? message.confidence;

  const act = async (action: 'acknowledge' | 'call-me') => {
    if (await send(() => actOnInboxMessage(id, action))) {
      setChosenScreen('followUp');
    }
  };

  const submit = async () => {
    const sent = await send(async () => {
      let latest = message;
      if (answer) {
        latest = await actOnInboxMessage(id, answer === 'understand' ? 'understand' : 'call-me');
        onChanged(latest);
        // The answer is with CareLoop now. If the rating fails, a retry must not send it again.
        setAnswer(null);
      }

      return rating === null ? latest : rateConfidence(id, rating);
    });
    if (sent) {
      setAnswer(null);
      setRating(null);
    }
  };

  return (
    <div className="phone" role="group" aria-label={t(`inbox.channel.${message.channel}`)}>
      <div className="phone-screen">
        <div className="phone-notch" />
        <div className="phone-status">
          <span>{formatClock(message.receivedAt)}</span>
          <span className="phone-battery" />
        </div>

        <div className="phone-app">
          {screen === 'home' && (
            <>
              <div className="app-top">
                <span>
                  {t(`inbox.app.appName.${message.channel === 'Portal' ? 'Portal' : 'App'}`)}
                </span>
                <span className="app-language">
                  <span>{t('inbox.app.language')}</span>
                </span>
              </div>
              <div className="app-hello">
                {t('inbox.app.hello', { name: message.patientDisplayName })}
              </div>

              <div className={`pcard ${reachedPatient ? 'pcard-key' : 'pcard-locked'}`}>
                <span className="label">{t('inbox.app.nextStep')}</span>
                <p className="message-body" lang={message.language}>
                  {message.body}
                </p>
                {reachedPatient ? (
                  <>
                    <button
                      type="button"
                      className="pbtn pbtn-primary"
                      disabled={busy}
                      onClick={() => void act('acknowledge')}
                    >
                      {t('inbox.app.acknowledge')}
                    </button>
                    <button
                      type="button"
                      className="plink"
                      disabled={busy}
                      onClick={() => void act('call-me')}
                    >
                      {t('inbox.app.anotherTime')}
                    </button>
                  </>
                ) : (
                  <p className="fine">{t('inbox.notAnswerable')}</p>
                )}
              </div>

              {reachedPatient && (
                <div className="pcard">
                  <span className="label">{t('inbox.app.talk')}</span>
                  <p>{t('inbox.app.talkBody')}</p>
                  <button
                    type="button"
                    className="pbtn"
                    disabled={busy}
                    onClick={() => void act('call-me')}
                  >
                    <Icon name="phone" />
                    {t('inbox.app.callMe')}
                  </button>
                </div>
              )}

              {message.caseTaskId && (
                <div className="pcard">
                  <span className="label">{t('inbox.app.yourCase')}</span>
                  <Link
                    className="plink"
                    to={`${ROUTES.caseStatus}?taskId=${encodeURIComponent(message.caseTaskId)}`}
                  >
                    {t('inbox.app.caseStatusLink')}
                  </Link>
                </div>
              )}
            </>
          )}

          {screen === 'followUp' && (
            <>
              <button type="button" className="app-back" onClick={() => setChosenScreen('home')}>
                <Icon name="left" />
                {t('inbox.app.home')}
              </button>

              <div className="done-hero">
                <span className="done-mark">
                  <Icon name="check" />
                </span>
                <h4>{t('inbox.app.thanks', { name: message.patientDisplayName })}</h4>
                <p>
                  {message.response === 'CallRequested' || message.callRequestCount > 0
                    ? t('inbox.app.thanksCallRequested')
                    : t('inbox.app.thanksAcknowledged')}
                </p>
              </div>

              <fieldset className="pcard">
                <legend className="label">{t('inbox.app.teachBackLabel')}</legend>
                <h4>{t('inbox.app.teachBackQuestion')}</h4>
                {ANSWERS.map((option) => (
                  <label key={option} className={`choice ${answer === option ? 'choice-on' : ''}`}>
                    <input
                      type="radio"
                      name={`answer-${id}`}
                      checked={answer === option}
                      disabled={busy}
                      onChange={() => setAnswer(option)}
                    />
                    {t(`inbox.app.answer.${option}`)}
                  </label>
                ))}
              </fieldset>

              <fieldset className="pcard">
                <legend className="rate-heading">{t('inbox.app.rateHeading')}</legend>
                <div className="scale">
                  {RATINGS.map((value) => (
                    <button
                      key={value}
                      type="button"
                      disabled={busy}
                      aria-label={t('inbox.app.rate', { rating: value })}
                      aria-pressed={shownRating === value}
                      onClick={() => setRating(value)}
                    >
                      {value}
                    </button>
                  ))}
                </div>
                <div className="scale-labels">
                  <span>{t('inbox.app.rateLow')}</span>
                  <span>{t('inbox.app.rateHigh')}</span>
                </div>
              </fieldset>

              <button
                type="button"
                className="pbtn pbtn-primary"
                disabled={busy || (answer === null && rating === null)}
                onClick={() => void submit()}
              >
                {t('inbox.app.send')}
              </button>
            </>
          )}

          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
