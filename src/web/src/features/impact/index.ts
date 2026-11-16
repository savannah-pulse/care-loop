/**
 * Impact (screen 3): loop-closure measures from `GET /metrics`, each with its numerator and
 * denominator, times to each step, cases by display stage, closures by reason and patient
 * confidence; and the with-and-without-escalation comparison from `POST /metrics/simulation`.
 */

export { ImpactPage } from './ImpactPage'
export { MEASURE_KEYS, STEP_KEYS, barItems, confidenceView, durationView, rateView } from './impact'
export type { BarItem, DurationView, MeasureKey, RateView, StepKey } from './impact'
