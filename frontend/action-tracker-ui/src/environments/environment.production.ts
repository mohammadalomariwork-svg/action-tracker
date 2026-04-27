export const environment = {
  production: true,
  apiBaseUrl: 'https://apps.ku.ac.ae/actiontrackerAPIs/api',
  apiUrl: 'https://apps.ku.ac.ae/actiontrackerAPIs/api',
  msalConfig: {
    auth: {
      clientId: '8fef5759-4a09-444c-b4b3-3eca4fc4834e',
      authority: 'https://login.microsoftonline.com/08fe1c0a-19f5-4f24-a662-fdd5dd460025',
      redirectUri: 'https://apps.ku.ac.ae/actiontracker/auth_fallback',
      postLogoutRedirectUri: 'https://apps.ku.ac.ae/actiontracker/',
    },
    cache: {
      cacheLocation: 'localStorage',
      storeAuthStateInCookie: false,
    },
  },
  msalScopes: ['api://8fef5759-4a09-444c-b4b3-3eca4fc4834e/access_as_user'],
};
