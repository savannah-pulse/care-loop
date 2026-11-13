import { Icon } from '../../components'
import type { CaseStage, TrackStatus } from '../../data'
import { DISPLAY_STEPS, currentStepIndex } from '../../domain'
import { t } from '../../i18n'

/** Props for {@link StageStepper}. */
export interface StageStepperProps {
  /** The case's stage in the API. */
  readonly stage: CaseStage
  /** The state of the case's four tracks, which decide the step during follow-up. */
  readonly tracks: readonly TrackStatus[]
}

/**
 * The bar of ten display stages on the case screen, from "Result received" to "Closed".
 *
 * @remarks
 * Steps before the current one are done, the current one is marked, and later ones are still to
 * come; a closed case has every step done. The current step follows rule C9 (see
 * `currentStepIndex`). Each step's state is also written out for screen readers.
 */
export function StageStepper({ stage, tracks }: StageStepperProps) {
  const current = currentStepIndex(stage, tracks)
  const closed = stage === 'Closed'
  return (
    <ol className="stepper" aria-label={t('stages.stepperLabel')}>
      {DISPLAY_STEPS.map((key, index) => {
        const done = index < current || closed
        const now = index === current && !closed
        const state = done ? t('stages.stepDone') : now ? t('stages.stepCurrent') : t('stages.stepToDo')
        return (
          <li key={key} className={done ? 's done' : now ? 's now' : 's'} aria-current={now ? 'step' : undefined}>
            <span className="dot" aria-hidden="true">
              {done ? <Icon name="check" /> : null}
            </span>
            {t(key)}
            <span className="visually-hidden">, {state}</span>
          </li>
        )
      })}
    </ol>
  )
}
