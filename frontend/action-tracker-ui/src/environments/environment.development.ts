// Replace all placeholders with real Azure App Registration values before running.
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7135/api',
  apiUrl: 'http://localhost:5000/api',
  msalConfig: {
    auth: {
      clientId: 'J-b8Q~B7wbDkMQytlEr2GYviJHittQn4d63zgcNA',        // placeholder — Azure App Registration Client ID
      authority: 'https://login.microsoftonline.com/08fe1c0a-19f5-4f24-a662-fdd5dd460025',  // placeholder
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
