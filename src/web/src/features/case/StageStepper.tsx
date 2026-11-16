import { useId } from 'react'
import { Icon } from '../../components'
import type { CaseDisplayLabel } from '../../data'
import { DISPLAY_LABELS, displayLabelText, displayStepIndex } from '../../domain'
import { WIDE_LAYOUT_QUERY, useMediaQuery } from '../../hooks'
import { t } from '../../i18n'

/** Props for {@link StageStepper}. */
export interface StageStepperProps {
  /** The case's display stage, as the API reports it. */
  readonly label: CaseDisplayLabel
}

interface StepsProps {
  readonly current: number
  readonly closed: boolean
}

function stepState(index: number, { current, closed }: StepsProps): 'done' | 'now' | 'toDo' {
  if (closed || index < current) {
    return 'done'
  }
  return index === current ? 'now' : 'toDo'
}

const STATE_WORDS = {
  done: 'stages.stepDone',
  now: 'stages.stepCurrent',
  toDo: 'stages.stepToDo',
} as const

function Steps({ steps, className }: { readonly steps: StepsProps; readonly className: string }) {
  return (
    <ol className={className}>
      {DISPLAY_LABELS.map((label, index) => {
        const state = stepState(index, steps)
        return (
          <li
            key={label}
            className={state === 'toDo' ? 's' : `s ${state}`}
            aria-current={state === 'now' ? 'step' : undefined}
          >
            <span className="dot" aria-hidden="true">
              {state === 'done' ? <Icon name="check" /> : null}
            </span>
            <span className="step-label">{displayLabelText(label)}</span>
            <span className="visually-hidden">{t('stages.stepAnnounce', { state: t(STATE_WORDS[state]) })}</span>
          </li>
        )
      })}
    </ol>
  )
}

function CompactSteps({ steps }: { readonly steps: StepsProps }) {
  const index = steps.closed ? DISPLAY_LABELS.length - 1 : steps.current
  const next = steps.closed ? undefined : DISPLAY_LABELS[steps.current + 1]
  return (
    <div className="stage-compact">
      <p className="stage-now">
        {t('stages.stepOf', {
          number: index + 1,
          total: DISPLAY_LABELS.length,
          label: displayLabelText(DISPLAY_LABELS[index]),
        })}
      </p>
      <span className="segments" aria-hidden="true">
        {DISPLAY_LABELS.map((label, position) => (
          <span key={label} className={position <= index ? 'seg reached' : 'seg'} />
        ))}
      </span>
      <p className="stage-next">{next ? t('stages.next', { label: displayLabelText(next) }) : t('stages.allDone')}</p>
      <details className="disclosure">
        <summary>
          {t('stages.seeAll')}
          <Icon name="down" />
        </summary>
        <Steps steps={steps} className="steps-list" />
      </details>
    </div>
  )
}

/**
 * Where the case is among the ten display stages, from "Result received" to "Closed".
 *
 * @remarks
 * On wide screens all ten steps sit side by side, with labels that wrap onto two lines and never
 * overlap. Below 900 px it is compact: "Step 7 of 10: Follow-up", a slim bar, the next step and a
 * "See all steps" list. Steps before the current one are done and later ones are still to come; a
 * closed case has every step done. The current step is the display label the API reports
 * (decision 19); the dashboard never works it out itself. Each step's state is also written out for
 * screen readers.
 */
export function StageStepper({ label }: StageStepperProps) {
  const wide = useMediaQuery(WIDE_LAYOUT_QUERY)
  const headingId = useId()
  const steps: StepsProps = { current: Math.max(0, displayStepIndex(label)), closed: label === 'Closed' }
  return (
    <section className="card stage-card" aria-labelledby={headingId}>
      <h2 className="visually-hidden" id={headingId}>
        {t('stages.stepperLabel')}
      </h2>
      {wide ? <Steps steps={steps} className="stepper" /> : <CompactSteps steps={steps} />}
    </section>
  )
}
