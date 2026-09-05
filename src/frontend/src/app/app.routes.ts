import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

/**
 * There are no authentication routes any more. Sign-in, sign-up, password reset and email
 * confirmation are all Microsoft's screens now — the application never sees a credential, so it has
 * no pages to show for one.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'todos' },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell.component').then(m => m.ShellComponent),
    children: [
      {
        path: 'todos',
        loadComponent: () => import('./todos/todos-page.component').then(m => m.TodosPageComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./users/users-list.component').then(m => m.UsersListComponent)
      },
      {
        path: 'users/:id',
        loadComponent: () => import('./users/user-detail.component').then(m => m.UserDetailComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('./profile/profile-page.component').then(m => m.ProfilePageComponent)
      }
    ]
  }
];
