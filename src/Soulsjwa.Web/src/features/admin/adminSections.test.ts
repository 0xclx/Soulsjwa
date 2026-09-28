import { describe, expect, it } from 'vitest'
import { ADMIN_SECTION_NAV, getAdminSection } from './adminSections'

describe('admin sections', () => {
  it('has a Sample data section at /admin/sample-data', () => {
    expect(ADMIN_SECTION_NAV).toContainEqual({
      section: 'sample-data',
      label: 'Sample data',
      to: '/admin/sample-data',
    })
    expect(getAdminSection('/admin/sample-data')).toBe('sample-data')
  })
})
