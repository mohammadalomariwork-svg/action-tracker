import {
  APP_INITIALIZER,
  ApplicationConfig,
  importProvidersFrom,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { MsalBroadcastService, MsalModule, MsalService } from '@azure/msal-angular';
import { InteractionType, PublicClientApplication } from '@azure/msal-browser';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { getMsalConfig } from './core/auth/msal.config';

// ── MSAL setup ───────────────────────────────────────────────────────────────
// The PublicClientApplication instance must be created *outside* appConfig so
// the same reference can be shared between MsalModule.forRoot() and the
// APP_INITIALIZER that calls initialize().
//
// MSAL Browser v3 requires initialize() to be awaited before any interactive
// method (loginPopup, acquireToken, …) is called. Without this, loginPopup
// throws "unintialized_public_client_application" and no popup ever opens.
const msalInstance = new PublicClientApplication(getMsalConfig());

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        loadingInterceptor,
        errorInterceptor,
      ])
    ),
    provideAnimations(),
    provideToastr({
      positionClass: 'toast-bottom-right',
      timeOut: 3000,
      preventDuplicates: true,
      progressBar: true,
    }),

    // ── MSAL (Microsoft Authentication Library) ──────────────────────────────
    // MsalModule is NgModule-based; importProvidersFrom bridges it into the
    // standalone provider array. Token exchange is handled manually by
    // AuthService.loginWithAzureAd(), so the protectedResourceMap is empty —
    // the MsalInterceptor will not intercept any HTTP requests.
    importProvidersFrom(
      MsalModule.forRoot(
        msalInstance,
        // Guard config — Popup interaction for the MSAL route guard (if used)
        {
          interactionType: InteractionType.Popup,
        },
        // Interceptor config — Popup interaction; no routes are protected here
        {
          interactionType: InteractionType.Popup,
          protectedResourceMap: new Map(),
        },
      ),
    ),

    // Initialize the MSAL PublicClientApplication before the app bootstraps.
    // This resolves the internal MSAL cache and completes any pending redirect
    // responses, making the instance ready for loginPopup / acquireToken calls.
    {
      provide: APP_INITIALIZER,
      useFactory: () => () => msalInstance.initialize(),
      multi: true,
    },

    // Explicitly provide MSAL services so they are available for injection
    // anywhere in the app without needing the full MSAL route guard setup.
    MsalService,
    MsalBroadcastService,
  ],
};
