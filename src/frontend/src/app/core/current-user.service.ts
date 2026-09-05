import { Injectable, inject, signal } from '@angular/core';
import { ApiClientService } from './api-client.provider';
import { AuthService } from './auth.service';

export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
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

  readonly user = this.currentUser.asReadonly();

  readonly userId = () => this.currentUser()?.id ?? null;
  readonly email = () => this.currentUser()?.email ?? this.auth.email() ?? '';
  readonly displayName = () => {
    const user = this.currentUser();

    return user ? `${user.firstName} ${user.lastName}`.trim() : (this.auth.name() ?? '');
  };

  readonly isAdmin = () => this.auth.isAdmin();

  async load(): Promise<CurrentUser | null> {
    if (this.currentUser()) {
      return this.currentUser();
    }

    const result = await this.apiClient.users.me.get();

    if (!result?.id) {
      return null;
    }

    const user: CurrentUser = {
      id: result.id,
      email: result.email ?? '',
      firstName: result.firstName ?? '',
      lastName: result.lastName ?? ''
    };

    this.currentUser.set(user);

    return user;
  }
}
