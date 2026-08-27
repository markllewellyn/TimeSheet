import { bootstrapApplication } from '@angular/platform-browser';
import { MSAL_INSTANCE } from '@azure/msal-angular';
import { AllCommunityModule, ModuleRegistry } from 'ag-grid-community';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { MSALInstanceFactory } from './app/core/auth/msal.factories';

ModuleRegistry.registerModules([AllCommunityModule]);

// MSAL v3+ requires instance.initialize() to complete, and the redirect response to be processed via
// handleRedirectPromise(), before the app renders - otherwise login works once then breaks on refresh.
// The instance built here is the one and only PublicClientApplication for the app: it overrides the
// MSAL_INSTANCE factory provider in appConfig (last provider for a token wins) rather than letting DI build
// a second, uninitialized instance.
const msalInstance = MSALInstanceFactory();

msalInstance
  .initialize()
  // Default (true) makes MSAL Browser do a full window.location navigation back to the page that started
  // login BEFORE this promise resolves - since that happens ahead of bootstrapApplication below (deliberately,
  // so the redirect response is consumed before the app renders), that auto-navigation cuts this page load
  // short and reloads from scratch at the origin page, outside any Angular-integrated navigation. Disabled so
  // this resolves right here at the callback URL instead; CurrentUserService navigates to /timesheet itself
  // on LOGIN_SUCCESS once the app has actually bootstrapped.
  .then(() => msalInstance.handleRedirectPromise({ navigateToLoginRequestUrl: false }))
  .then(() =>
    bootstrapApplication(App, {
      providers: [...appConfig.providers, { provide: MSAL_INSTANCE, useValue: msalInstance }],
    }),
  )
  .catch((err) => console.error(err));
