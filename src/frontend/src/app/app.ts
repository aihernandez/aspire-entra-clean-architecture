import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (auth.error()) {
      <main class="grid min-h-dvh place-items-center bg-slate-50 px-4">
        <div class="max-w-md rounded-xl border border-slate-200 bg-white p-6 text-center shadow-sm">
          <h1 class="text-lg font-semibold text-slate-900">Sign-in is unavailable</h1>
          <p class="mt-2 text-sm text-slate-600" role="alert">{{ auth.error() }}</p>
          <button type="button" (click)="retry()"
            class="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white">
            Try again
          </button>
        </div>
      </main>
    } @else {
      <router-outlet />
    }
  `
})
export class App {
  protected readonly auth = inject(AuthService);

  protected retry(): void {
    window.location.reload();
  }
}
