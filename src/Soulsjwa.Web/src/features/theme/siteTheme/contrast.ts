/**
 * WCAG 2.1 contrast ratio between two #rrggbb colours, in [1, 21]. Mirrors
 * `SiteThemeValidator.ContrastRatio` on the backend exactly, so the admin
 * editor's live readout agrees with the server's authoritative 4.5:1 (WCAG
 * AA) check — this is a UI hint only; the server remains the source of
 * truth and is what actually rejects an invalid palette.
 */
export function contrastRatio(hex1: string, hex2: string): number {
  const l1 = relativeLuminance(hex1)
  const l2 = relativeLuminance(hex2)
  const lighter = Math.max(l1, l2)
  const darker = Math.min(l1, l2)
  return (lighter + 0.05) / (darker + 0.05)
}

const HEX_PATTERN = /^#[0-9a-fA-F]{6}$/

export const isValidHex = (value: string): boolean => HEX_PATTERN.test(value)

function relativeLuminance(hex: string): number {
  const { r, g, b } = parseHex(hex)
  return 0.2126 * linearize(r) + 0.7152 * linearize(g) + 0.0722 * linearize(b)
}

function linearize(channel: number): number {
  const s = channel / 255
  return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4)
}

function parseHex(hex: string): { r: number; g: number; b: number } {
  const v = hex.replace('#', '')
  return {
    r: parseInt(v.slice(0, 2), 16),
    g: parseInt(v.slice(2, 4), 16),
    b: parseInt(v.slice(4, 6), 16),
  }
}
