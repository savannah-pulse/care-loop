import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { installFakeApi, resetFakeApi, takeUnanswered } from './api'
import { DEFAULT_SCREEN_WIDTH, fillBrowserGaps, setScreenWidth } from './browser'

// Before any test imports the API client, which keeps the fetch it finds when it is created.
fillBrowserGaps()
installFakeApi()

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
