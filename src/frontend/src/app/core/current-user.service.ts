import { Injectable, inject, signal } from '@angular/core';
import { ApiClientService } from './api-client.provider';
import { AuthService } from './auth.service';

export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  permissions: string[];
}

/**
 * Resolves the caller's **local** user id by calling `GET /users/me`.
 *
 * This round-trip is unavoidable under Entra ID and is worth understanding: the access token
 * carries the directory's `oid`, but every id in this application's own data — the owner of a todo,
 * the target of a permission check — is the local `User.Id`. They are different values on purpose,
 * and only the API knows the mapping. The first call also triggers just-in-time provisioning
 * server-side, so it doubles as "make sure I exist here".
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly apiClient = inject(ApiClientService).client;
  private readonly auth = inject(AuthService);

  private readonly currentUser = signal<CurrentUser | null>(null);
  private pendingLoad: Promise<CurrentUser | null> | null = null;

  readonly user = this.currentUser.asReadonly();

  readonly userId = () => this.currentUser()?.id ?? null;
  readonly email = () => this.currentUser()?.email ?? this.auth.email() ?? '';
  readonly displayName = () => {
    const user = this.currentUser();

    return user ? `${user.firstName} ${user.lastName}`.trim() : (this.auth.name() ?? '');
  };

  readonly roles = () => this.currentUser()?.roles ?? [];
  readonly canReadUsers = () => this.currentUser()?.permissions.includes('users:read-all') ?? false;
  readonly canManageUsers = () => this.currentUser()?.permissions.includes('users:manage') ?? false;

  load(): Promise<CurrentUser | null> {
    if (this.currentUser()) {
      return Promise.resolve(this.currentUser());
    }

    this.pendingLoad ??= this.fetchCurrentUser().finally(() => {
      this.pendingLoad = null;
    });

    return this.pendingLoad;
  }

  private async fetchCurrentUser(): Promise<CurrentUser | null> {
    const result = await this.apiClient.users.me.get();

    if (!result?.id) {
      throw new Error('The API did not return a current user.');
    }

    const user: CurrentUser = {
      id: result.id,
      email: result.email ?? '',
      firstName: result.firstName ?? '',
      lastName: result.lastName ?? '',
      roles: result.roles ?? [],
      permissions: result.permissions ?? []
    };

    this.currentUser.set(user);

    return user;
  }
}
