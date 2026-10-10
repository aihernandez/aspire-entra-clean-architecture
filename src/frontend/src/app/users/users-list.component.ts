import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClientService } from '../core/api-client.provider';
import { extractErrorMessage } from '../core/api-error';
import { untypedNumber } from '../core/kiota-untyped';
import { CurrentUserService } from '../core/current-user.service';

interface UserRow {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
}

/**
 * Two things about this screen are consequences of Entra ID owning identity, and both
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
  changeDetection: ChangeDetectionStrategy.Eager,
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

      @if (error()) {
        <p role="alert" class="text-sm text-rose-600">{{ error() }}</p>
      }
      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else {
        <div class="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th class="px-4 py-3 font-medium">Name</th>
                <th class="px-4 py-3 font-medium">Email</th>
                <th class="px-4 py-3 font-medium">Status</th>
                <th class="px-4 py-3 font-medium">Actions</th>
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
                  <td class="px-4 py-3">
                    <span [class]="user.isActive ? 'text-emerald-700' : 'text-slate-500'">
                      {{ user.isActive ? 'Active' : 'Deactivated' }}
                    </span>
                  </td>
                  <td class="px-4 py-3">
                    <div class="flex flex-wrap items-center gap-3">
                      <a [routerLink]="['/users', user.id]" class="font-medium text-indigo-600 hover:underline">Edit</a>
                      @if (user.isActive && user.id !== currentUser.userId()) {
                        @if (confirmDeleteId() === user.id) {
                          <span class="text-slate-600">Deactivate local access?</span>
                          <button type="button" (click)="deleteUser(user.id)" [disabled]="busyId() === user.id"
                            class="font-medium text-rose-700 disabled:opacity-50">Confirm</button>
                          <button type="button" (click)="confirmDeleteId.set(null)" class="text-slate-600">Cancel</button>
                        } @else {
                          <button type="button" (click)="confirmDeleteId.set(user.id)"
                            class="font-medium text-rose-700 hover:underline">Delete</button>
                        }
                      }
                    </div>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="4" class="px-4 py-6 text-center text-slate-500">Nobody has signed in yet.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600">
          <p>{{ totalCount() }} total · Page {{ pageNumber() }} of {{ totalPages() }}</p>
          <nav aria-label="User pages" class="flex gap-2">
            <button type="button" (click)="loadPage(pageNumber() - 1)"
              [disabled]="pageNumber() <= 1"
              class="rounded-lg border border-slate-300 px-3 py-1.5 disabled:cursor-not-allowed disabled:opacity-50">
              Previous
            </button>
            <button type="button" (click)="loadPage(pageNumber() + 1)"
              [disabled]="pageNumber() >= totalPages()"
              class="rounded-lg border border-slate-300 px-3 py-1.5 disabled:cursor-not-allowed disabled:opacity-50">
              Next
            </button>
          </nav>
        </div>
      }
    </div>
  `
})
export class UsersListComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService).client;
  protected readonly currentUser = inject(CurrentUserService);

  protected readonly users = signal<UserRow[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly pageNumber = signal(1);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);
  private readonly pageSize = 20;
  private requestId = 0;

  ngOnInit(): void {
    void this.loadPage(1);
  }

  protected async loadPage(page: number): Promise<void> {
    if (page < 1 || (this.totalCount() > 0 && page > this.totalPages())) return;
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    this.confirmDeleteId.set(null);

    try {
      const result = await this.apiClient.users.get({
        // Kiota's TypeScript preview types int32 query parameters as string — see LESSONS-LEARNED.
        queryParameters: {
          pageNumber: String(page),
          pageSize: String(this.pageSize),
          includeInactive: true
        }
      });

      if (requestId !== this.requestId) return;
      this.users.set(
        (result?.items ?? []).map(item => ({
          id: item.id ?? '',
          email: item.email ?? '',
          firstName: item.firstName ?? '',
          lastName: item.lastName ?? '',
          isActive: item.isActive ?? true
        }))
      );

      this.totalCount.set(untypedNumber(result?.totalCount) ?? 0);
      this.pageNumber.set(page);
    } catch (error) {
      if (requestId !== this.requestId) return;
      this.error.set(extractErrorMessage(error, 'Could not load users.'));
    } finally {
      if (requestId === this.requestId) this.loading.set(false);
    }
  }

  protected async deleteUser(id: string): Promise<void> {
    if (this.confirmDeleteId() !== id || this.busyId()) return;
    this.busyId.set(id);
    this.error.set(null);
    try {
      await this.apiClient.users.byUserId(id).delete();
      this.users.update(rows => rows.map(row => row.id === id ? { ...row, isActive: false } : row));
      this.confirmDeleteId.set(null);
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not deactivate the user.'));
    } finally {
      this.busyId.set(null);
    }
  }
}
