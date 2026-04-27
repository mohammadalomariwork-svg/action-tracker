import {
  APP_INITIALIZER,
  ApplicationConfig,
  importProvidersFrom,
  inject,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { MsalBroadcastService, MsalModule, MsalService } from '@azure/msal-angular';
import { InteractionType, PublicClientApplication } from '@azure/msal-browser';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { getMsalConfig } from './core/auth/msal.config';
import { AuthService } from './core/services/auth.service';
import { PermissionStateService } from './features/permissions/services/permission-state.service';
import { ThemeService } from './services/theme.service';

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
        {
          interactionType: InteractionType.Redirect,
        },
        {
          interactionType: InteractionType.Redirect,
          protectedResourceMap: new Map(),
        },
      ),
    ),

    // Initialize MSAL and complete any pending redirect response before the
    // app bootstraps. When the browser lands back at /auth_fallback#code=...,
    // handleRedirectPromise() exchanges the code for an Azure AD access token,
    // which we then swap for a local JWT via /api/auth/azure-login.
    {
      provide: APP_INITIALIZER,
      useFactory: () => {
        const authService = inject(AuthService);
        return async () => {
          await msalInstance.initialize();
          try {
            const result = await msalInstance.handleRedirectPromise();
            if (result?.accessToken) {
              await firstValueFrom(authService.loginWithAzureAd(result.accessToken));
            }
          } catch {
            // Swallow — login page will surface the error on next interaction.
          }
        };
      },
      multi: true,
    },

    // Apply persisted theme before the first render.
    {
      provide: APP_INITIALIZER,
      useFactory: () => {
        const themeService = inject(ThemeService);
        return () => themeService.initTheme();
      },
      multi: true,
    },

    // Pre-load permissions before the first route guard runs.
    // Only fires when the user already has a valid session (e.g. hard refresh).
    // Errors are swallowed so a permissions API failure never blocks bootstrap.
    {
      provide: APP_INITIALIZER,
      useFactory: () => {
        const authService     = inject(AuthService);
        const permissionState = inject(PermissionStateService);
        return () => {
          if (!authService.isAuthenticated()) return Promise.resolve();
          return firstValueFrom(permissionState.loadPermissions()).catch(() => null);
        };
      },
      multi: true,
    },

    // Explicitly provide MSAL services so they are available for injection
    // anywhere in the app without needing the full MSAL route guard setup.
    MsalService,
    MsalBroadcastService,
  ],
};
