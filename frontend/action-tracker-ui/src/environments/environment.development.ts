// Replace all placeholders with real Azure App Registration values before running.
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7135/api',
  apiUrl: 'http://localhost:5000/api',
  msalConfig: {
    auth: {
      clientId: '',        // placeholder — Azure App Registration Client ID
      authority: 'https://login.microsoftonline.com/<TENANT_ID>',  // placeholder
      redirectUri: 'http://localhost:4200/auth/azure-callback',
      postLogoutRedirectUri: 'http://localhost:4200/login',
    },
    cache: {
      cacheLocation: 'localStorage',
      storeAuthStateInCookie: false,
    },
  },
  msalScopes: ['openid', 'profile', 'email', 'User.Read'],
};
