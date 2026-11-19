import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'
import { answer, installFakeApi, problem, resetFakeApi, takeUnanswered } from './api'
import { DEFAULT_SCREEN_WIDTH, fillBrowserGaps, setScreenWidth } from './browser'

// Before any test imports the API client, which keeps the fetch it finds when it is created.
fillBrowserGaps()
installFakeApi()

// AI is switched off unless a test switches it on, as it is by default in the API: the worklist then
// and the impact page then show no AI summary, and tests about anything else need not answer for it.
beforeEach(() => {
  answer('GET /ai/role-briefing', problem(409, 'AI is switched off'))
  answer('GET /ai/impact-summary', problem(409, 'AI is switched off'))
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  setScreenWidth(DEFAULT_SCREEN_WIDTH)
  const unanswered = takeUnanswered()
  resetFakeApi()
  if (unanswered.length > 0) {
    throw new Error(`The screen called routes the test did not answer: ${[...new Set(unanswered)].join(', ')}`)
  }
})
