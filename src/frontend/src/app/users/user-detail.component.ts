import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiClientService } from '../core/api-client.provider';
import { extractErrorMessage } from '../core/api-error';
import { CurrentUserService } from '../core/current-user.service';

interface UserDetail {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
}

/**
 * The application owns local access state. Profile details and role assignments stay in Entra ID.
 */
@Component({
  selector: 'app-user-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="space-y-6">
      <a routerLink="/users" class="text-sm text-indigo-600 hover:underline">&larr; Back to users</a>

      @if (error()) {
        <p role="alert" class="text-sm text-rose-600">{{ error() }}</p>
      }
      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
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

        @if (currentUser.canManageUsers()) {
        <div class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 class="font-semibold text-slate-900">Edit application access</h2>
          <p class="mt-1 text-sm text-slate-500">Deactivating access keeps the user's historical data.</p>
          <label class="mt-4 flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" [checked]="draftIsActive()" (change)="changeStatus($event)"
              [disabled]="busy() || detail.id === currentUser.userId()"
              class="size-4 rounded border-slate-300 text-indigo-600" />
            Active in this application
          </label>
          @if (detail.id === currentUser.userId()) {
            <p class="mt-2 text-xs text-slate-500">You cannot deactivate your own account.</p>
          }
          <button type="button" (click)="saveStatus()" [disabled]="busy() || !hasChanges()"
            class="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50">
            {{ busy() ? 'Saving…' : 'Save changes' }}
          </button>
          @if (saved()) {
            <p role="status" class="mt-2 text-sm text-emerald-700">Access updated.</p>
          }
        </div>
        }

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
  protected readonly currentUser = inject(CurrentUserService);

  protected readonly user = signal<UserDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly draftIsActive = signal(true);
  protected readonly busy = signal(false);
  protected readonly saved = signal(false);
  protected readonly hasChanges = computed(() =>
    this.user() !== null && this.user()!.isActive !== this.draftIsActive());

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
        this.draftIsActive.set(result.isActive ?? true);
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

  protected changeStatus(event: Event): void {
    this.draftIsActive.set((event.target as HTMLInputElement).checked);
    this.saved.set(false);
  }

  protected async saveStatus(): Promise<void> {
    const user = this.user();
    if (!user || !this.hasChanges() || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.saved.set(false);
    try {
      await this.apiClient.users.byUserId(user.id).status.put({ isActive: this.draftIsActive() });
      this.user.set({ ...user, isActive: this.draftIsActive() });
      this.saved.set(true);
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not update application access.'));
    } finally {
      this.busy.set(false);
    }
  }
}
