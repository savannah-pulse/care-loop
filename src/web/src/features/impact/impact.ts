import type { DurationMeasure, Metrics, RateMeasure } from '../../data'
import { formatRate, formatSpan, measuredDuration, toCount, toNumber } from '../../format'

/**
 * The rate measures of screen 3, in the order the screen lists them.
 *
 * @remarks
 * Keys of `MetricsResponse.measures`. Each rate comes with the cases it counts and the cases it is
 * out of; a rate with nobody to count is "not measured yet", never 0%.
 */
export const MEASURE_KEYS = [
  'nextStepOnTime',
  'understanding',
  'acknowledgement',
  'closure',
  'referralAcceptedAndAttended',
  'appointmentsAttended',
  'treatmentStartedOnTime',
  'signedEvidence',
] as const

/** A rate measure the screen has words for. */
export type MeasureKey = (typeof MEASURE_KEYS)[number]

/**
 * The steps whose median time from the result screen 3 shows, in order.
 *
 * @remarks
 * Keys of `MetricsResponse.timeToStep`. Medians are clock time, not working days.
 */
export const STEP_KEYS = ['acknowledgement', 'consultation', 'appointment', 'careStarted', 'closure'] as const

/** A step the screen has words for. */
export type StepKey = (typeof STEP_KEYS)[number]

/** A rate ready to show: the percentage when there is one, and its counts. */
export interface RateView {
  /** The percentage, for example `86%`; absent when nobody was eligible. */
  readonly rate?: string
  /** The cases the measure counts. */
  readonly numerator: number
  /** The cases it is out of. */
  readonly denominator: number
}

/**
 * Prepares a rate measure for the screen.
 *
 * @param measure - The measure from the API, if it sent one.
 * @returns Its counts, and its percentage only when its population is not empty. The rate is worked
 * out from the counts when the API leaves it out.
 */
export function rateView(measure: RateMeasure | undefined): RateView {
  const numerator = toCount(measure?.numerator)
  const denominator = toCount(measure?.denominator)
  if (denominator <= 0) {
    return { numerator, denominator }
  }
  const given = toNumber(measure?.rate)
  return { rate: formatRate(given ?? numerator / denominator), numerator, denominator }
}

/** A median time to a step, ready to show. */
export interface DurationView {
  /** The median in words, for example `1 day 20 h`; absent when no case reached the step. */
  readonly median?: string
  /** How many cases reached the step. */
  readonly cases: number
}

/**
 * Prepares a median time to a step for the screen.
 *
 * @param measure - The measure from the API, if it sent one.
 * @returns The median in exact words, or none when it was not measured or is not a duration, and
 * how many cases it is taken from.
 */
export function durationView(measure: DurationMeasure | undefined): DurationView {
  const milliseconds = measuredDuration(measure?.median)
  return {
    median: milliseconds === undefined ? undefined : formatSpan(milliseconds),
    cases: toCount(measure?.denominator),
  }
}

/** One bar of a bar list. */
export interface BarItem {
  /** A stable key. */
  readonly key: string
  /** The label beside the bar. */
  readonly label: string
  /** The count the bar shows. */
  readonly value: number
}

/**
 * Lists counts as bars, known keys first in their order, then anything else the API sent.
 *
 * @param values - Counts by key, as the API sends them.
 * @param known - The keys the screen knows, in the order to show them.
 * @param label - Words for a key.
 * @returns One bar per key, so a new key from the API still shows.
 */
export function barItems(
  values: Readonly<Record<string, number | string | RateMeasure>> | undefined,
  known: readonly string[],
  label: (key: string) => string,
): BarItem[] {
  const source = values ?? {}
  const keys = [...known, ...Object.keys(source).filter((key) => !known.includes(key))]
  return keys.map((key) => {
    const value = source[key]
    const count = typeof value === 'object' && value !== null ? toCount(value.numerator) : toCount(value)
    return { key, label: label(key), value: count }
  })
}

/**
 * Reads the patient-confidence figures of the metrics.
 *
 * @param metrics - The metrics from the API.
 * @returns The average rating with one decimal place when anyone rated, and how many answered out
 * of how many were told.
 */
export function confidenceView(metrics: Pick<
  Metrics,
  'averageConfidence' | 'confidenceResponses' | 'confidenceDenominator' | 'confidenceRate'
>): { readonly average?: number; readonly responses: number; readonly told: number; readonly rate?: string } {
  return {
    average: toNumber(metrics.averageConfidence),
    responses: toCount(metrics.confidenceResponses),
    told: toCount(metrics.confidenceDenominator),
    rate: formatRate(metrics.confidenceRate),
  }
}
