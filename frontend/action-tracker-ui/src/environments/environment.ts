// Replace all placeholders with real Azure App Registration values before running.
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7135/api',
  apiUrl: 'https://localhost:7135/api',
  msalConfig: {
    auth: {
      clientId: '4b1ceb12-9cf9-4d79-a919-5428a59aa5eb',        // placeholder — Azure App Registration Client ID
      authority: 'https://login.microsoftonline.com/08fe1c0a-19f5-4f24-a662-fdd5dd460025',  // placeholder
      redirectUri: 'http://localhost:4200/auth_fallback',
      postLogoutRedirectUri: 'http://localhost:4200/dashboards',
    },
    cache: {
      cacheLocation: 'localStorage',
      storeAuthStateInCookie: false,
    },
  },
  msalScopes: ['api://4b1ceb12-9cf9-4d79-a919-5428a59aa5eb/access_as_user'],
};
