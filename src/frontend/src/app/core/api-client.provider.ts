import { Injectable, inject } from '@angular/core';
import { DefaultRequestAdapter } from '@microsoft/kiota-bundle';
import { createApiClient, type ApiClient } from '../api-client/apiClient';
import { ApiAuthenticationProvider } from './api-authentication-provider';
import { API_BASE_URL } from './api-config';

/**
 * Wraps the generated ApiClient so Angular's DI never holds the client itself as a provided
 * value. The client is a JS Proxy (Kiota's `apiClientProxifier`) that throws for any property
 * it doesn't recognize as a navigation segment — including `ngOnDestroy`, which Angular's
 * injector probes on every provided value to decide whether to register a destroy hook. That
 * throw happens during injector resolution and silently aborts bootstrap (blank page, no
 * console output beyond the thrown error). A plain wrapper class sidesteps it: Angular only
 * ever introspects this service instance, never the Proxy underneath.
 */
@Injectable({ providedIn: 'root' })
export class ApiClientService {
  private readonly authProvider = inject(ApiAuthenticationProvider);

  readonly client: ApiClient = createApiClient(this.createRequestAdapter());

  private createRequestAdapter(): DefaultRequestAdapter {
    const requestAdapter = new DefaultRequestAdapter(this.authProvider);
    requestAdapter.baseUrl = API_BASE_URL;

    return requestAdapter;
  }
}
