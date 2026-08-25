import {
  IPublicClientApplication,
  InteractionType,
  LogLevel,
  PublicClientApplication,
} from '@azure/msal-browser';
import {
  MsalGuardConfiguration,
  MsalInterceptorConfiguration,
} from '@azure/msal-angular';
import { environment } from '../../../environments/environment';

/**
 * Redirect (not Popup) is used throughout - preferred over Popup in corporate/Entra environments to avoid
 * popup-blocker and third-party-cookie issues (see the plan's Authentication section).
 */
export function MSALInstanceFactory(): IPublicClientApplication {
  return new PublicClientApplication({
    auth: {
      clientId: environment.entra.clientId,
      authority: environment.entra.authority,
      redirectUri: '/',
      postLogoutRedirectUri: '/',
    },
    cache: {
      cacheLocation: 'localStorage',
    },
    system: {
      loggerOptions: {
        loggerCallback: () => {},
        logLevel: LogLevel.Warning,
        piiLoggingEnabled: false,
      },
    },
  });
}

export function MSALGuardConfigFactory(): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    authRequest: {
      scopes: [environment.entra.apiScope],
    },
  };
}

/**
 * Attaches a bearer token to every call to the Api - matched by URL pattern, not by base address, since the
 * Api origin differs between local dev (proxied same-origin via proxy.conf.json) and deployed environments.
 */
export function MSALInterceptorConfigFactory(): MsalInterceptorConfiguration {
  const protectedResourceMap = new Map<string, Array<string> | null>();
  protectedResourceMap.set(`${environment.apiBaseUrl}/*`, [environment.entra.apiScope]);

  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap,
  };
}
