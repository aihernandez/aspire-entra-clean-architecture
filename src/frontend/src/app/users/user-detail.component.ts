import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiClientService } from '../core/api-client.provider';
import { extractErrorMessage } from '../core/api-error';

interface UserDetail {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
}

/**
 * Read-only, for the same reasons as the list: the directory owns the person's details, and role
 * assignment happens in the tenant rather than in this application.
 */
@Component({
  selector: 'app-user-detail',
  imports: [RouterLink],
  template: `
    <div class="space-y-6">
      <a routerLink="/users" class="text-sm text-indigo-600 hover:underline">&larr; Back to users</a>

      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (error()) {
        <p class="text-sm text-rose-600">{{ error() }}</p>
      } @else if (user(); as detail) {
        <header>
          <h1 class="text-2xl font-semibold tracking-tight text-slate-900">
            {{ detail.firstName }} {{ detail.lastName }}
          </h1>
          <p class="mt-1 text-sm text-slate-500">{{ detail.email }}</p>
        </header>

        <div class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <dl class="grid gap-5 sm:grid-cols-2">
            <div>
              <dt class="text-xs font-medium uppercase tracking-wide text-slate-500">Status</dt>
              <dd class="mt-1 text-sm">
                @if (detail.isActive) {
                  <span class="rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700">Active</span>
                } @else {
                  <span class="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-600">Deactivated</span>
                }
              </dd>
            </div>
            <div>
              <dt class="text-xs font-medium uppercase tracking-wide text-slate-500">
                Application user id
              </dt>
              <dd class="mt-1 font-mono text-xs text-slate-600">{{ detail.id }}</dd>
            </div>
          </dl>
        </div>

        <div class="rounded-xl border border-slate-200 bg-slate-50 p-4">
          <p class="text-sm text-slate-600">
            Profile details and role assignments are managed in Microsoft Entra ID.
          </p>
        </div>
      }
    </div>
  `
})
export class UserDetailComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService).client;
  private readonly route = inject(ActivatedRoute);

  protected readonly user = signal<UserDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');

    if (!id) {
      this.error.set('No user id supplied.');
      this.loading.set(false);
      return;
    }

    try {
      const result = await this.apiClient.users.byUserId(id).get();

      if (result?.id) {
        this.user.set({
          id: result.id,
          email: result.email ?? '',
          firstName: result.firstName ?? '',
          lastName: result.lastName ?? '',
          isActive: result.isActive ?? true
        });
      }
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not load the user.'));
    } finally {
      this.loading.set(false);
    }
  }
}
