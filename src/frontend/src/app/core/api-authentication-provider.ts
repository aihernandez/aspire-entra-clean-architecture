import { Injectable, inject } from '@angular/core';
import type { AuthenticationProvider, RequestInformation } from '@microsoft/kiota-abstractions';
import { AuthService } from './auth.service';

/**
 * The single place an access token is attached to an outgoing API call.
 *
 * There is no refresh logic here any more: token lifetime, renewal and caching are MSAL's job now,
 * and `acquireTokenSilent` either returns a valid token or escalates to interaction on its own.
 * The hand-rolled refresh-token rotation this used to perform went away with ASP.NET Core Identity.
 */
@Injectable({ providedIn: 'root' })
export class ApiAuthenticationProvider implements AuthenticationProvider {
  private readonly auth = inject(AuthService);

  async authenticateRequest(request: RequestInformation): Promise<void> {
    const accessToken = await this.auth.getAccessToken();

    if (accessToken) {
      request.headers.tryAdd('Authorization', `Bearer ${accessToken}`);
    }
  }
}
