import { bootstrapApplication } from '@angular/platform-browser';
import { MSAL_INSTANCE } from '@azure/msal-angular';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { MSALInstanceFactory } from './app/core/auth/msal.factories';

// MSAL v3+ requires instance.initialize() to complete, and the redirect response to be processed via
// handleRedirectPromise(), before the app renders - otherwise login works once then breaks on refresh.
// The instance built here is the one and only PublicClientApplication for the app: it overrides the
// MSAL_INSTANCE factory provider in appConfig (last provider for a token wins) rather than letting DI build
// a second, uninitialized instance.
const msalInstance = MSALInstanceFactory();

msalInstance
  .initialize()
  .then(() => msalInstance.handleRedirectPromise())
  .then(() =>
    bootstrapApplication(App, {
      providers: [...appConfig.providers, { provide: MSAL_INSTANCE, useValue: msalInstance }],
    }),
  )
  .catch((err) => console.error(err));
