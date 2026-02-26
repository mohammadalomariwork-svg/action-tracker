import { ApplicationConfig, importProvidersFrom, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
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
        new PublicClientApplication(getMsalConfig()),
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
    // Explicitly provide MSAL services so they are available for injection
    // anywhere in the app without needing the full MSAL route guard setup.
    MsalService,
    MsalBroadcastService,
  ],
};
