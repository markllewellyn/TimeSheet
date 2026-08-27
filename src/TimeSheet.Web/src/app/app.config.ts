import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  MSAL_GUARD_CONFIG,
  MSAL_INSTANCE,
  MsalBroadcastService,
  MsalGuard,
  MsalService,
} from '@azure/msal-angular';
import { routes } from './app.routes';
import { localAuthInterceptor } from './core/auth/local-auth.interceptor';
import { MSALGuardConfigFactory, MSALInstanceFactory } from './core/auth/msal.factories';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // MsalInterceptor is deliberately NOT registered here: with SSO not yet configured (placeholder tenant),
    // it would match every /api/* call via its protectedResourceMap and silently redirect the whole tab to
    // Entra to acquire a token, before the request ever reaches the backend - including local-account calls
    // that don't need one. localAuthInterceptor (functional) attaches the local Bearer token instead, and is
    // a no-op if no local session exists. Re-add MsalInterceptor once real Entra SSO config is in place.
    provideHttpClient(withInterceptors([localAuthInterceptor]), withFetch()),
    {
      provide: MSAL_INSTANCE,
      useFactory: MSALInstanceFactory,
    },
    {
      provide: MSAL_GUARD_CONFIG,
      useFactory: MSALGuardConfigFactory,
    },
    MsalService,
    MsalGuard,
    MsalBroadcastService,
  ],
};
