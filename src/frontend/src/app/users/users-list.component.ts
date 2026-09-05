import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClientService } from '../core/api-client.provider';
import { extractErrorMessage } from '../core/api-error';
import { untypedNumber } from '../core/kiota-untyped';

interface UserRow {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
}

/**
 * Read-only. Two things about this screen are consequences of Entra ID owning identity, and both
 * are stated in the UI rather than hidden:
 *
 * 1. It lists people from this application's own mirror table, so it can only show those who have
 *    signed in at least once. Enumerating the directory would need Microsoft Graph and tenant-wide
 *    application permissions this template deliberately does not request.
 * 2. There is no "make administrator" action, because the application is not where roles live.
 *    Assignment happens in the Enterprise Application, by someone who administers the tenant.
 */
@Component({
  selector: 'app-users-list',
  imports: [RouterLink],
  template: `
    <div class="space-y-6">
      <header>
        <h1 class="text-2xl font-semibold tracking-tight text-slate-900">Users</h1>
        <p class="mt-1 text-sm text-slate-500">
          People who have signed in to this application at least once.
        </p>
      </header>

      <div class="rounded-xl border border-amber-200 bg-amber-50 p-4">
        <p class="text-sm text-amber-900">
          Roles are managed in Microsoft Entra ID, not here. To grant or revoke access, assign the
          app role in the Enterprise Application's <strong>Users and groups</strong> blade.
        </p>
      </div>

      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (error()) {
        <p class="text-sm text-rose-600">{{ error() }}</p>
      } @else {
        <div class="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th class="px-4 py-3 font-medium">Name</th>
                <th class="px-4 py-3 font-medium">Email</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (user of users(); track user.id) {
                <tr class="hover:bg-slate-50">
                  <td class="px-4 py-3">
                    <a [routerLink]="['/users', user.id]" class="font-medium text-indigo-600 hover:underline">
                      {{ user.firstName }} {{ user.lastName }}
                    </a>
                  </td>
                  <td class="px-4 py-3 text-slate-600">{{ user.email }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="2" class="px-4 py-6 text-center text-slate-500">Nobody has signed in yet.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <p class="text-xs text-slate-500">{{ totalCount() }} total</p>
      }
    </div>
  `
})
export class UsersListComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService).client;

  protected readonly users = signal<UserRow[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);

    try {
      const result = await this.apiClient.users.get({
        // Kiota's TypeScript preview types int32 query parameters as string — see LESSONS-LEARNED.
        queryParameters: { pageNumber: String(1), pageSize: String(50) }
      });

      this.users.set(
        (result?.items ?? []).map(item => ({
          id: item.id ?? '',
          email: item.email ?? '',
          firstName: item.firstName ?? '',
          lastName: item.lastName ?? ''
        }))
      );

      this.totalCount.set(untypedNumber(result?.totalCount) ?? 0);
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not load users.'));
    } finally {
      this.loading.set(false);
    }
  }
}
