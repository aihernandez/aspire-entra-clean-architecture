/**
 * Base URL of the backend API. Browsers can't use .NET Aspire's server-side service discovery,
 * so this points directly at Web.Api's listen address — the AppHost's "http" launch profile
 * (src/backend/Web.Api/Properties/launchSettings.json) fixes it at localhost:5000 for local dev.
 */
export const API_BASE_URL = 'http://localhost:5000';
