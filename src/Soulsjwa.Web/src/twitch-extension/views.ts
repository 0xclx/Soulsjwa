/** The Twitch views this bundle ships, one entry page each (see `twitch-extension/*.html`). */
export const EXTENSION_VIEWS = ['panel', 'component', 'mobile', 'config', 'live_config'] as const
export type ExtensionView = (typeof EXTENSION_VIEWS)[number]
