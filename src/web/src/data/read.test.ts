import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { answer, json, sentRequests } from '../testing/api'
import type { ApiOutcome } from './outcome'
import { FRESH_READ_MS, LIVE_REFRESH_MS, useApiRead, type Loader, type ReadOptions, type ReadState } from './read'
import { useMetrics, useSimulation } from './reads'

/** One call of a {@link scriptedLoader}: the signal it was given, and a way to answer it. */
interface Call<T> {
  readonly signal: AbortSignal
  readonly answer: (outcome: ApiOutcome<T>) => Promise<void>
}

/** A loader the test answers call by call, so it decides when each read comes back. */
function scriptedLoader<T>() {
  const calls: Call<T>[] = []
  const load: Loader<T> = (signal) =>
    new Promise<ApiOutcome<T>>((resolve) => {
      calls.push({
        signal,
        answer: async (outcome) => {
          await act(async () => resolve(outcome))
        },
      })
    })
  return { load, calls }
}

/** Opens a screen that reads with `load`, and keeps every state the screen was given. */
function openScreen<T>(load: Loader<T>, options?: ReadOptions) {
  const states: ReadState<T>[] = []
  const screen = renderHook(() => {
    const read = useApiRead(load, undefined, options)
    states.push(read.state)
    return read
  })
  return { ...screen, states }
}

async function wait(ms: number): Promise<void> {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

function showPage(visibility: DocumentVisibilityState): void {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => visibility })
  act(() => {
    document.dispatchEvent(new Event('visibilitychange'))
  })
}

function focusWindow(): void {
  act(() => {
    window.dispatchEvent(new Event('focus'))
  })
}

const LIVE: ReadOptions = { live: true }
const FAILED: ApiOutcome<number> = { ok: false, failure: { kind: 'error' } }

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  Reflect.deleteProperty(document, 'visibilityState')
})

describe('keeping a read up to date', () => {
  it('reads again every 30 seconds while the page is visible, keeping the data on screen meanwhile', async () => {
    const { load, calls } = scriptedLoader<number>()
    const { result, states } = openScreen(load, LIVE)
    await calls[0].answer({ ok: true, data: 1 })

    await wait(LIVE_REFRESH_MS - 1)
    expect(calls).toHaveLength(1)
    await wait(1)
    expect(calls).toHaveLength(2)
    expect(result.current.state).toEqual({ status: 'ready', data: 1 })

    await calls[1].answer({ ok: true, data: 2 })
    expect(result.current.state).toEqual({ status: 'ready', data: 2 })
    await wait(LIVE_REFRESH_MS)
    expect(calls).toHaveLength(3)
    expect(states.slice(states.findIndex((state) => state.status === 'ready')).map((state) => state.status)).not.toContain(
      'loading',
    )
  })

  it('waits while the page is hidden, and reads as soon as it is shown again', async () => {
    const { load, calls } = scriptedLoader<number>()
    openScreen(load, LIVE)
    await calls[0].answer({ ok: true, data: 1 })

    showPage('hidden')
    await wait(5 * LIVE_REFRESH_MS)
    expect(calls).toHaveLength(1)

    showPage('visible')
    expect(calls).toHaveLength(2)
  })

  it('reads again when the window regains focus, unless the last answer is only seconds old', async () => {
    const { load, calls } = scriptedLoader<number>()
    openScreen(load, LIVE)
    await calls[0].answer({ ok: true, data: 1 })

    await wait(FRESH_READ_MS - 1)
    focusWindow()
    expect(calls).toHaveLength(1)

    await wait(1)
    focusWindow()
    expect(calls).toHaveLength(2)
  })

  it('keeps the data when a quiet read fails, and tries again next time', async () => {
    const { load, calls } = scriptedLoader<number>()
    const { result } = openScreen(load, LIVE)
    await calls[0].answer({ ok: true, data: 1 })

    await wait(LIVE_REFRESH_MS)
    await calls[1].answer(FAILED)
    expect(result.current.state).toEqual({ status: 'ready', data: 1 })

    await wait(LIVE_REFRESH_MS)
    await calls[2].answer({ ok: true, data: 3 })
    expect(result.current.state).toEqual({ status: 'ready', data: 3 })
  })

  it('shows a failure only while there is nothing else to show, and tries again only when it may pass', async () => {
    const failing = scriptedLoader<number>()
    const { result } = openScreen(failing.load, LIVE)
    await failing.calls[0].answer(FAILED)
    expect(result.current.state).toMatchObject({ status: 'failed', failure: { kind: 'error' } })
    const shown = result.current.state

    await wait(LIVE_REFRESH_MS)
    await failing.calls[1].answer(FAILED)
    expect(result.current.state).toBe(shown)
    await wait(LIVE_REFRESH_MS)
    await failing.calls[2].answer({ ok: true, data: 1 })
    expect(result.current.state).toEqual({ status: 'ready', data: 1 })

    const refused = scriptedLoader<number>()
    openScreen(refused.load, LIVE)
    await refused.calls[0].answer({ ok: false, failure: { kind: 'forbidden', status: 403 } })
    await wait(5 * LIVE_REFRESH_MS)
    focusWindow()
    expect(refused.calls).toHaveLength(1)
  })

  it('never starts a read while another is still on its way', async () => {
    const { load, calls } = scriptedLoader<number>()
    openScreen(load, LIVE)
    await wait(5 * LIVE_REFRESH_MS)
    focusWindow()
    expect(calls).toHaveLength(1)

    await calls[0].answer({ ok: true, data: 1 })
    await wait(LIVE_REFRESH_MS)
    expect(calls).toHaveLength(2)
    await wait(5 * LIVE_REFRESH_MS)
    showPage('hidden')
    showPage('visible')
    focusWindow()
    expect(calls).toHaveLength(2)

    await calls[1].answer({ ok: true, data: 2 })
    await wait(LIVE_REFRESH_MS)
    expect(calls).toHaveLength(3)
  })

  it('cancels the read on its way, and reads no more, once the screen closes', async () => {
    const { load, calls } = scriptedLoader<number>()
    const { unmount } = openScreen(load, LIVE)
    await calls[0].answer({ ok: true, data: 1 })
    await wait(LIVE_REFRESH_MS)
    expect(calls).toHaveLength(2)

    unmount()
    expect(calls[1].signal.aborted).toBe(true)
    await wait(5 * LIVE_REFRESH_MS)
    showPage('visible')
    focusWindow()
    expect(calls).toHaveLength(2)
  })

  it('never repeats a read the screen did not ask to keep up to date', async () => {
    const { load, calls } = scriptedLoader<number>()
    openScreen(load)
    await calls[0].answer({ ok: true, data: 1 })

    await wait(5 * LIVE_REFRESH_MS)
    showPage('hidden')
    showPage('visible')
    focusWindow()
    expect(calls).toHaveLength(1)
  })

  it('keeps Impact’s numbers up to date, but never repeats the comparison, which replays a whole cohort', async () => {
    answer('GET /metrics', json({}))
    answer('POST /metrics/simulation', json({}))
    renderHook(() => [useMetrics(), useSimulation()])
    await wait(1)
    expect(sentRequests('GET /metrics')).toHaveLength(1)

    await wait(LIVE_REFRESH_MS)
    expect(sentRequests('GET /metrics')).toHaveLength(2)
    await wait(5 * LIVE_REFRESH_MS)
    focusWindow()
    expect(sentRequests('POST /metrics/simulation')).toHaveLength(1)
  })
})
