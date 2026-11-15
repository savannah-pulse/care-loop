import { describe, expect, it } from 'vitest'
import { barItems, confidenceView, durationView, rateView } from './impact'

describe('impact measures (C5, C10)', () => {
  it('shows each rate with its counts, and no rate when nobody was eligible', () => {
    expect(rateView({ numerator: 4, denominator: 7, rate: 0.5714285714285714 })).toEqual({
      rate: '57%',
      numerator: 4,
      denominator: 7,
    })
    expect(rateView({ numerator: 0, denominator: 1, rate: 0 })).toEqual({ rate: '0%', numerator: 0, denominator: 1 })
    expect(rateView({ numerator: 0, denominator: 0, rate: null })).toEqual({ numerator: 0, denominator: 0 })
    expect(rateView({ numerator: '3', denominator: '4' })).toEqual({ rate: '75%', numerator: 3, denominator: 4 })
    expect(rateView(undefined)).toEqual({ numerator: 0, denominator: 0 })
  })

  it('shows the median time to each step as clock time, and none when no case reached it', () => {
    expect(durationView({ denominator: 1, median: '1.20:00:00' })).toEqual({ median: '1 day 20 h', cases: 1 })
    expect(durationView({ denominator: '4', median: 'PT4H' })).toEqual({ median: '4 h', cases: 4 })
    expect(durationView({ denominator: 0, median: null })).toEqual({ median: undefined, cases: 0 })
    expect(durationView({ denominator: 2, median: 'soon' })).toEqual({ median: undefined, cases: 2 })
  })

  it('lists counts with the known keys first, and keeps anything new the API sends', () => {
    const label = (key: string): string => key.toLowerCase()
    expect(barItems({ Closed: 1, FollowUp: '2', Transferred: 3 }, ['FollowUp', 'Closed', 'Consultation'], label)).toEqual([
      { key: 'FollowUp', label: 'followup', value: 2 },
      { key: 'Closed', label: 'closed', value: 1 },
      { key: 'Consultation', label: 'consultation', value: 0 },
      { key: 'Transferred', label: 'transferred', value: 3 },
    ])
    expect(barItems({ OwnerAssigned: { numerator: 9, denominator: 9, rate: 1 } }, ['OwnerAssigned'], label)[0].value).toBe(9)
  })

  it('reads patient confidence as participation and an average', () => {
    expect(
      confidenceView({ averageConfidence: 4, confidenceResponses: 1, confidenceDenominator: 4, confidenceRate: 0.25 }),
    ).toEqual({ average: 4, responses: 1, told: 4, rate: '25%' })
    expect(
      confidenceView({ averageConfidence: null, confidenceResponses: 0, confidenceDenominator: 0, confidenceRate: null }),
    ).toEqual({ average: undefined, responses: 0, told: 0, rate: undefined })
  })
})
