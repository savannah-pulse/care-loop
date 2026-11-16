import { describe, expect, it, vi } from 'vitest'

vi.mock('./i18n', async (original) => ({ ...await original<typeof import('./i18n')>(), language: 'de' }))

describe('formatting locale', () => {
  it('follows the active i18n language rather than a hard-coded one', async () => {
    const { numberText, rateText } = await import('./format')
    expect(rateText(0.625)).toMatch(/^62,5\s%$/)
    expect(numberText(1.25)).toBe('1,25')
  })
})
