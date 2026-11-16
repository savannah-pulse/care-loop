import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, createApi, isHeaderSafe, queryString } from './client'

afterEach(() => vi.unstubAllGlobals())
const identity = { user: 'SYN-STAFF-1', role: 'clinician', facility: 'SYN-FACILITY-1' }

describe('dashboard REST boundary', () => {
  it('encodes filters without dropping zero or false', () => {
    expect(queryString({ skip: 0, flagged: false, reason: 'follow-up & review', empty: '', missing: undefined }))
      .toBe('?skip=0&flagged=false&reason=follow-up+%26+review')
  })

  it('carries identity, access reason and cancellation on reads', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response('[]'))
    vi.stubGlobal('fetch', fetch)
    const controller = new AbortController()
    await createApi(identity).cases({ facilityId: 'SYN-FACILITY-2', reason: 'Covering clinic', skip: 0 }, controller.signal)
    expect(fetch).toHaveBeenCalledWith('/api/cases?facilityId=SYN-FACILITY-2&reason=Covering+clinic&skip=0', expect.objectContaining({
      signal: controller.signal, method: 'GET',
      headers: { 'X-Demo-User': identity.user, 'X-Demo-Role': identity.role, 'X-Demo-Facility': identity.facility },
    }))
  })

  it('reads exactly the case page it was asked for from a successful JSON body', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('[{"caseId":"case-1"}]')))
    await expect(createApi(identity).cases({})).resolves.toEqual([{ caseId: 'case-1' }])
  })

  it('reports a 200 response that is not JSON as a localized error, not a raw syntax error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>gateway</html>')))
    await expect(createApi(identity).metrics()).rejects.toThrow('The API returned a response that could not be read')
  })

  it('refuses an identity a header cannot carry before sending, without calling it a network failure', async () => {
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    const api = createApi({ ...identity, user: 'سارة' })
    await expect(api.cases({})).rejects.toThrow('characters that a request cannot carry')
    expect(fetch).not.toHaveBeenCalled()
  })

  it('detects characters above Latin-1 and control characters in header values', () => {
    expect(isHeaderSafe('SYN-STAFF-1 (Zoë)')).toBe(true)
    expect(isHeaderSafe('سارة')).toBe(false)
    expect(isHeaderSafe('line\nbreak')).toBe(false)
    expect(isHeaderSafe('tab\there')).toBe(false)
    expect(isHeaderSafe('\u0085')).toBe(false)
  })

  it('approves exactly the edited text, including whitespace', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response('{}'))
    vi.stubGlobal('fetch', fetch)
    const text = '  Human-reviewed facts.\n\nNext action unchanged.  '
    await createApi(identity).approve('case-1', 'draft-1', text)
    expect(fetch).toHaveBeenCalledWith('/api/cases/case-1/handoff-summary/draft-1/approval',
      expect.objectContaining({ method: 'POST', body: JSON.stringify({ approvedText: text }) }))
  })

  it('acknowledges a specific escalation without sending case changes', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response('{}'))
    vi.stubGlobal('fetch', fetch)
    await createApi(identity).acknowledge('case-1', 'notice/2', 'Covering clinic')
    expect(fetch).toHaveBeenCalledWith('/api/cases/case-1/escalations/notice%2F2/acknowledgement?reason=Covering+clinic',
      expect.objectContaining({ method: 'POST', body: '{}' }))
  })

  it.each([403, 409, 501, 503])('surfaces HTTP %i instead of producing a successful fallback', async (status) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('internal details', { status })))
    await expect(createApi(identity).draft('case-1')).rejects.toMatchObject({ status })
    await expect(createApi(identity).draft('case-1')).rejects.toBeInstanceOf(ApiError)
  })

  it('surfaces network failures while preserving cancellation', async () => {
    const failure = new Error('connection refused')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(failure))
    await expect(createApi(identity).metrics()).rejects.toMatchObject({ cause: failure })
    const controller = new AbortController()
    controller.abort()
    await expect(createApi(identity).metrics(controller.signal)).rejects.toBe(failure)
  })
})
