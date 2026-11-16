import { useCallback } from 'react'
import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useResource } from './useResource'

function deferred<T>() {
  let resolve: (value: T) => void = () => {}
  const promise = new Promise<T>((res) => { resolve = res })
  return { promise, resolve }
}

type Loader = (key: string, signal: AbortSignal) => Promise<string>

/** Mirrors production use: a load callback that is stable for a given key. */
function useKeyed(key: string, loader: Loader, scope?: string) {
  const load = useCallback((signal: AbortSignal) => loader(key, signal), [key, loader])
  return useResource(key, load, scope)
}

describe('useResource', () => {
  it('aborts the previous read when the key changes and ignores its late result', async () => {
    const first = deferred<string>()
    const second = deferred<string>()
    const signals: AbortSignal[] = []
    const loader: Loader = (key, signal) => { signals.push(signal); return key === 'a' ? first.promise : second.promise }
    const { result, rerender } = renderHook(({ key }) => useKeyed(key, loader), { initialProps: { key: 'a' } })
    rerender({ key: 'b' })
    expect(signals[0].aborted).toBe(true)
    await act(async () => { first.resolve('late result for a'); await first.promise })
    expect(result.current.data).toBeNull()
    expect(result.current.loading).toBe(true)
    await act(async () => { second.resolve('result for b'); await second.promise })
    expect(result.current.data).toBe('result for b')
  })

  it('never shows another key\'s data unless the scope is shared', async () => {
    const loader: Loader = (key) => Promise.resolve(key)
    const { result, rerender } = renderHook(({ key }) => useKeyed(key, loader), { initialProps: { key: 'reader-1' } })
    await waitFor(() => expect(result.current.data).toBe('reader-1'))
    rerender({ key: 'reader-2' })
    expect(result.current).toEqual({ data: null, loading: true, error: null })
  })

  it('keeps previous data while reloading within the same scope', async () => {
    const reload = deferred<string>()
    const loader: Loader = (key) => key === 'a/1' ? Promise.resolve('first') : reload.promise
    const { result, rerender } = renderHook(({ key }) => useKeyed(key, loader, 'a'), { initialProps: { key: 'a/1' } })
    await waitFor(() => expect(result.current.data).toBe('first'))
    rerender({ key: 'a/2' })
    expect(result.current).toEqual({ data: 'first', loading: true, error: null })
    await act(async () => { reload.resolve('second'); await reload.promise })
    expect(result.current).toEqual({ data: 'second', loading: false, error: null })
  })

  it('drops previous data when the scope changes', async () => {
    const loader: Loader = (key) => key === 'a/1' ? Promise.resolve('first') : new Promise<string>(() => {})
    const { result, rerender } = renderHook(({ key, scope }) => useKeyed(key, loader, scope), { initialProps: { key: 'a/1', scope: 'a' } })
    await waitFor(() => expect(result.current.data).toBe('first'))
    rerender({ key: 'b/1', scope: 'b' })
    expect(result.current.data).toBeNull()
  })

  it('reports a failed reload as an error without stale data', async () => {
    const loader: Loader = (key) => key === 'a/1' ? Promise.resolve('first') : Promise.reject(new Error('Reload failed'))
    const { result, rerender } = renderHook(({ key }) => useKeyed(key, loader, 'a'), { initialProps: { key: 'a/1' } })
    await waitFor(() => expect(result.current.data).toBe('first'))
    rerender({ key: 'a/2' })
    await waitFor(() => expect(result.current.error).toBe('Reload failed'))
    expect(result.current.data).toBeNull()
  })
})
