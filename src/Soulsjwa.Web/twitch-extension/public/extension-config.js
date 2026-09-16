// Deployment-specific settings of the Twitch extension bundle. This file is
// replaced when an admin downloads the zip from /admin/twitch-extension: the
// API writes the deployment's origin here, so one build serves any host.
// Empty in development, where the dev server proxies /api to the backend.
window.SOULSJWA_TWITCH_EXTENSION = { apiUrl: '' }
