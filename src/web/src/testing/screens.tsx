import { render, type RenderResult } from '@testing-library/react'
import { userEvent, type UserEvent } from '@testing-library/user-event'
import { vi } from 'vitest'
import { App } from '../App'
import { forgetOpenDrafts } from '../features/summary'
import { DEFAULT_STAFF_MEMBER, forgetAccessReasons, signInAs } from '../identity'
import { DEFAULT_SCREEN_WIDTH, setScreenWidth } from './browser'
import { TEST_NOW } from './builders'

/** Where, as whom and how wide {@link renderApp} opens the dashboard. */
export interface OpenAt {
  /** The address to open, for example `#/cases/case-1/summary`. */
  readonly at: string
  /** The `id` of the person to sign in as, from the prototype's staff list; the nurse navigator by default. */
  readonly as?: string
  /** The window's width in CSS pixels; {@link DEFAULT_SCREEN_WIDTH}, the wide layout, by default. */
  readonly width?: number
}

/** The dashboard as a screen test renders it, with a user to act on it. */
export interface RenderedApp extends RenderResult {
  /** Clicks, types and chooses as a person would, one event at a time. */
  readonly user: UserEvent
}

/**
 * Opens the whole dashboard (header, routing and screens) at an address, signed in as one of the
 * prototype's staff, with every API call answered by the fake API in `api.ts`.
 *
 * @param options - Where to open it, as whom and how wide the window is.
 * @returns The rendered dashboard and a user to act on it.
 *
 * @remarks
 * Each test starts afresh: reasons given for other teams' cases and open summary drafts left by an
 * earlier test are forgotten. The clock reads {@link TEST_NOW}, so due times read the same on any
 * day; only `Date` is fixed and timers still run. The test setup puts the real clock back after
 * each test.
 */
export function renderApp({ at, as = DEFAULT_STAFF_MEMBER.id, width = DEFAULT_SCREEN_WIDTH }: OpenAt): RenderedApp {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(TEST_NOW)
  forgetAccessReasons()
  forgetOpenDrafts()
  signInAs(as)
  setScreenWidth(width)
  window.location.hash = at
  const user = userEvent.setup()
  return { ...render(<App />), user }
}
