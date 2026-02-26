import { BrowserCacheLocation, Configuration } from '@azure/msal-browser';
import { environment } from '../../../environments/environment';

/**
 * Builds and returns the MSAL `Configuration` object used to initialise a
 * `PublicClientApplication` instance.
 *
 * All values are read from `environment.msalConfig` — no secrets are
 * hardcoded here. Replace the placeholder `clientId` and `authority` values
 * in the environment files before running against a real Azure tenant.
 *
 * @returns A fully typed MSAL {@link Configuration} ready for
 *          `new PublicClientApplication(getMsalConfig())`.
 */
export function getMsalConfig(): Configuration {
  const { auth, cache } = environment.msalConfig;

  return {
    auth: {
      /**
       * The Application (client) ID of the Azure App Registration.
       * Set this in environment.ts before use.
       */
      clientId: auth.clientId,

      /**
       * The authority URL of the form
       * `https://login.microsoftonline.com/<TENANT_ID>`.
       * Set the tenant ID in environment.ts before use.
       */
      authority: auth.authority,

      /** URI the browser is redirected to after a successful interactive login. */
      redirectUri: auth.redirectUri,

      /** URI the browser is redirected to after a successful logout. */
      postLogoutRedirectUri: auth.postLogoutRedirectUri,
    },
    cache: {
      /**
       * Where MSAL stores tokens and auth state.
       * `localStorage` persists the session across browser tabs and reloads;
       * `sessionStorage` limits it to the current tab.
       */
      cacheLocation: cache.cacheLocation as BrowserCacheLocation,

      /**
       * When `true`, MSAL also writes the session state to a cookie so that
       * IE 11 / Edge Legacy can share state across tabs.
       * Disabled by default — set to `true` only if IE 11 support is required.
       */
      storeAuthStateInCookie: cache.storeAuthStateInCookie,
    },
  };
}
