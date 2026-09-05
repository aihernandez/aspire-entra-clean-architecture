import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * There is no login page to redirect to: with Entra ID the sign-in screen belongs to Microsoft.
 * An unauthenticated visitor is sent straight there, and comes back to the URL they asked for.
 */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);

  if (auth.isAuthenticated()) {
    return true;
  }

  await auth.signIn();

  return false;
};
