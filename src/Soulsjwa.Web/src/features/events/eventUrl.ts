export const getEventUrlIdentifier = (eventId: string, urlAlias?: string | null) =>
  urlAlias || eventId

export const getEventPath = (eventId: string, urlAlias?: string | null) =>
  `/events/${getEventUrlIdentifier(eventId, urlAlias)}`
