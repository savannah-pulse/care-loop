import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

afterEach(() => vi.unstubAllGlobals())

function stubFetch() {
  const fetch = vi.fn().mockImplementation(() => Promise.resolve(new Response('[]')))
  vi.stubGlobal('fetch', fetch)
  return fetch
}

describe('staff identity form', () => {
  it('refuses characters a header cannot carry with a field message, and sends nothing', async () => {
    const fetch = stubFetch()
    const user = userEvent.setup()
    render(<App />)
    const field = screen.getByLabelText('Staff reference')
    await user.type(field, 'سارة')
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText(/characters a request cannot carry/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply identity' })).toBeDisabled()
    fireEvent.submit(field.closest('form')!)
    expect(fetch).not.toHaveBeenCalled()
    expect(screen.queryByText(/could not be reached/)).not.toBeInTheDocument()
  })

  it('checks the facility field too, and clears the message once corrected', async () => {
    const user = userEvent.setup()
    render(<App />)
    await user.type(screen.getByLabelText('Staff reference'), 'SYN-STAFF-1')
    const facility = screen.getByLabelText('Facility')
    fireEvent.change(facility, { target: { value: 'SYN\u0007FAC' } })
    expect(facility).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('button', { name: 'Apply identity' })).toBeDisabled()
    await user.clear(facility)
    await user.type(facility, 'SYN-FACILITY-1')
    expect(facility).not.toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('button', { name: 'Apply identity' })).toBeEnabled()
  })

  it('applies a valid identity, sends it as headers and marks the current screen', async () => {
    const fetch = stubFetch()
    const user = userEvent.setup()
    render(<App />)
    await user.type(screen.getByLabelText('Staff reference'), 'SYN-STAFF-1')
    await user.type(screen.getByLabelText('Facility'), 'SYN-FACILITY-1')
    await user.click(screen.getByRole('button', { name: 'Apply identity' }))
    await waitFor(() => expect(fetch).toHaveBeenCalled())
    expect(fetch.mock.calls[0][1].headers).toMatchObject({ 'X-Demo-User': 'SYN-STAFF-1', 'X-Demo-Facility': 'SYN-FACILITY-1' })
    expect(screen.getByRole('button', { name: 'Worklist' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('button', { name: 'Impact' })).not.toHaveAttribute('aria-current')
  })
})
