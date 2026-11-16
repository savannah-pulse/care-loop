import { describe, expect, it } from 'vitest'
import { findStaffByReference, sameReference } from './staff'

describe('matching people', () => {
  it('matches a reference with or without its FHIR type, ignoring letter case', () => {
    expect(sameReference('rekha-varghese', 'Practitioner/rekha-varghese')).toBe(true)
    expect(sameReference('Practitioner/Joy-Bautista', 'joy-bautista')).toBe(true)
    expect(sameReference(' p-100245891 ', 'Patient/p-100245891')).toBe(true)
  })

  it('never matches different people or missing references', () => {
    expect(sameReference('rekha-varghese', 'sara-al-hammadi')).toBe(false)
    expect(sameReference('', '')).toBe(false)
    expect(sameReference(undefined, null)).toBe(false)
    expect(sameReference('facility-lead:family-health-centre', 'joy-bautista')).toBe(false)
  })

  it('finds a person from either form of reference', () => {
    expect(findStaffByReference('Practitioner/joy-bautista')?.name).toBe('Joy Bautista')
    expect(findStaffByReference('layla-haddad')?.facilityId).toBe('general-hospital')
    expect(findStaffByReference('someone-else')).toBeUndefined()
  })
})
