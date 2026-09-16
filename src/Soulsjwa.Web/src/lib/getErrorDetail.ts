/**
 * Extract a human-readable message from an Axios-style error response:
 * prefers `detail`, falls back to flattening ASP.NET's `errors` validation
 * problem dictionary (field -> messages[]) when no `detail` is present, and
 * falls back to a caller-supplied message when neither is present.
 */
export function getErrorDetail(err: unknown, fallback: string): string {
  const e = err as {
    response?: { data?: { detail?: string; errors?: Record<string, string[]> } }
  }
  const data = e?.response?.data
  if (data?.detail) return data.detail
  if (data?.errors) {
    const messages = Object.values(data.errors).flat()
    if (messages.length > 0) return messages.join(' ')
  }
  return fallback
}
