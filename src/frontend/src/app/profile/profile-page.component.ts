import { Component, inject, signal } from '@angular/core';
import { CurrentUserService } from '../core/current-user.service';
import { AuthService } from '../core/auth.service';

/**
 * Read-only by design.
 *
 * Name, email and password all live in Entra ID now. Letting somebody edit them here would write to
 * a cache that the very next request overwrites from the directory — a field that silently reverts
 * is worse than a field that isn't there. Changes go through the organization's own directory
 * self-service.
 */
@Component({
  selector: 'app-profile-page',
  imports: [],
  template: `
    <div class="space-y-6">
      <header>
        <h1 class="text-2xl font-semibold tracking-tight text-slate-900">Profile</h1>
        <p class="mt-1 text-sm text-slate-500">
          These details come from your organization's directory.
        </p>
      </header>

      <div class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <dl class="grid gap-5 sm:grid-cols-2">
          <div>
            <dt class="text-xs font-medium uppercase tracking-wide text-slate-500">Name</dt>
            <dd class="mt-1 text-sm text-slate-900">{{ displayName() || '—' }}</dd>
          </div>
          <div>
            <dt class="text-xs font-medium uppercase tracking-wide text-slate-500">Email</dt>
            <dd class="mt-1 text-sm text-slate-900">{{ email() || '—' }}</dd>
          </div>
          <div>
            <dt class="text-xs font-medium uppercase tracking-wide text-slate-500">Roles</dt>
            <dd class="mt-1 flex flex-wrap gap-1.5">
              @for (role of roles(); track role) {
                <span class="rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-medium text-indigo-700">
                  {{ role }}
                </span>
              } @empty {
                <span class="text-sm text-slate-500">No roles assigned</span>
              }
            </dd>
          </div>
          <div>
            <dt class="text-xs font-medium uppercase tracking-wide text-slate-500">
              Application user id
            </dt>
            <dd class="mt-1 font-mono text-xs text-slate-600">{{ userId() || '—' }}</dd>
          </div>
        </dl>
      </div>

      <div class="rounded-xl border border-slate-200 bg-slate-50 p-4">
        <p class="text-sm text-slate-600">
          To change your name, email address or password, use your organization's account settings.
          This application never stores a password.
        </p>
      </div>

      @if (error()) {
        <p class="text-sm text-rose-600">{{ error() }}</p>
      }
    </div>
  `
})
export class ProfilePageComponent {
  private readonly currentUser = inject(CurrentUserService);
  private readonly auth = inject(AuthService);

  protected readonly error = signal<string | null>(null);

  protected readonly displayName = () => this.currentUser.displayName();
  protected readonly email = () => this.currentUser.email();
  protected readonly userId = () => this.currentUser.userId();
  protected readonly roles = () => this.auth.roles();

  constructor() {
    void this.currentUser.load().catch(() => this.error.set('Could not load your profile.'));
  }
}
