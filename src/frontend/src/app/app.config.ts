import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
  inject
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { AuthService } from './core/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),

    // MSAL has to be initialised, and any redirect response consumed, before the router runs —
    // otherwise the guard asks about an account that has not been restored yet, and every MSAL
    // call throws "uninitialized_public_client_application".
    provideAppInitializer(() => inject(AuthService).initialize()),

    provideRouter(routes)
  ]
};
