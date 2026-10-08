/**
 * Local Aspire uses the API's fixed port. In Azure, Front Door forwards /api/* to Web.Api
 * and removes the /api prefix before the request reaches the container.
 */
export const API_BASE_URL =
  window.location.hostname === 'localhost'
    ? 'http://localhost:5000'
    : `${window.location.origin}/api`;
