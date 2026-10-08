import { Injectable, signal } from '@angular/core';
import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo
} from '@azure/msal-browser';
import { API_BASE_URL } from './api-config';

interface AuthConfigResponse {
  enabled: boolean;
  clientId: string;
  authority: string;
  apiScope: string;
}

/**
 * Wraps `@azure/msal-browser` directly rather than using `@azure/msal-angular`.
 *
 * The reason is that this application talks to its API through a Kiota-generated client, not
 * Angular's `HttpClient` — so `MsalInterceptor`, which is most of what msal-angular provides,
 * cannot see any of the traffic. What is left (a guard) is a few lines. Attaching the token
 * happens in `ApiAuthenticationProvider`, the one seam Kiota gives us for it.
 *
 * The tenant, client id and scope are **not** compiled into this app: they are fetched from
 * `GET /auth-config` at startup. In a template that matters — otherwise every clone inherits
 * whichever directory the previous author happened to configure.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private msal: PublicClientApplication | null = null;
  private account: AccountInfo | null = null;
  private config: AuthConfigResponse | null = null;

  /**
   * True when the API reports no Entra ID configuration, i.e. it is running on its development
   * authentication scheme. MSAL is never loaded in that mode.
   */
  private devBypass = false;

  private readonly ready = signal(false);
  private readonly initializationError = signal<string | null>(null);

  readonly isReady = this.ready.asReadonly();
  readonly error = this.initializationError.asReadonly();

  /**
   * Called once at bootstrap. MSAL must be initialised, and the redirect response processed,
   * before any other call — otherwise every one of them throws.
   */
  async initialize(): Promise<void> {
    this.ready.set(false);
    this.initializationError.set(null);
    this.devBypass = false;
    this.account = null;
    this.msal = null;
    this.config = null;

    try {
      this.config = await this.fetchConfig();

      if (!this.config.enabled) {
        this.devBypass = true;
        return;
      }

      this.msal = new PublicClientApplication({
        auth: {
          clientId: this.config.clientId,
          authority: this.config.authority,
          redirectUri: window.location.origin
        },
        cache: { cacheLocation: 'localStorage' }
      });

      await this.msal.initialize();

      const redirectResponse = await this.msal.handleRedirectPromise();

      this.account = redirectResponse?.account ?? this.msal.getAllAccounts()[0] ?? null;

      if (this.account) {
        this.msal.setActiveAccount(this.account);
      }
    } catch {
      this.initializationError.set('Could not load sign-in settings. Check the API and try again.');
    } finally {
      this.ready.set(true);
    }
  }

  private async fetchConfig(): Promise<AuthConfigResponse> {
    const response = await fetch(`${API_BASE_URL}/auth-config`, {
      signal: AbortSignal.timeout(10000)
    });

    if (!response.ok) {
      throw new Error(`Authentication configuration returned ${response.status}`);
    }

    const config = (await response.json()) as Partial<AuthConfigResponse>;

    if (typeof config?.enabled !== 'boolean' ||
        (config.enabled && (!config.clientId || !config.authority || !config.apiScope))) {
      throw new Error('Authentication configuration is incomplete');
    }

    return config as AuthConfigResponse;
  }

  isAuthenticated(): boolean {
    return this.devBypass || this.account !== null;
  }

  email(): string | null {
    if (this.devBypass) {
      return 'dev.user@localhost';
    }

    return this.account?.username ?? null;
  }

  name(): string | null {
    if (this.devBypass) {
      return 'Dev User';
    }

    return this.account?.name ?? null;
  }

  async signIn(): Promise<void> {
    if (this.initializationError() || this.devBypass || !this.msal || !this.config) {
      return;
    }

    await this.msal.loginRedirect({ scopes: [this.config.apiScope] });
  }

  async signOut(): Promise<void> {
    if (this.devBypass || !this.msal) {
      return;
    }

    await this.msal.logoutRedirect({ account: this.account ?? undefined });
  }

  /**
   * An access token for **this API's** scope. Returns null in development-bypass mode: the API's
   * development scheme authenticates by configuration, so there is no token to send and no
   * Authorization header is added.
   */
  async getAccessToken(): Promise<string | null> {
    if (this.devBypass || !this.msal || !this.account || !this.config) {
      return null;
    }

    try {
      const result = await this.msal.acquireTokenSilent({
        scopes: [this.config.apiScope],
        account: this.account
      });

      return result.accessToken;
    } catch (error) {
      // The silent path fails when consent, MFA, or a Conditional Access policy needs the user —
      // including after a Continuous Access Evaluation revocation. The only cure is interaction.
      if (error instanceof InteractionRequiredAuthError) {
        await this.msal.acquireTokenRedirect({
          scopes: [this.config.apiScope],
          account: this.account
        });
      }

      return null;
    }
  }
}
