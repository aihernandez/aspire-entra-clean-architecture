import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClientService } from '../core/api-client.provider';
import { CurrentUserService } from '../core/current-user.service';
import { extractErrorMessage } from '../core/api-error';
import type { TodoResponse } from '../api-client/models';

@Component({
  selector: 'app-todos-page',
  imports: [FormsModule],
  templateUrl: './todos-page.component.html'
})
export class TodosPageComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService).client;
  private readonly currentUser = inject(CurrentUserService);

  protected readonly todos = signal<TodoResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected description = '';

  async ngOnInit(): Promise<void> {
    // The local user id is not in the token — it has to be fetched once. See CurrentUserService.
    await this.currentUser.load();
    await this.reload();
  }

  protected async createTodo(): Promise<void> {
    const userId = this.currentUser.userId();

    if (!userId) {
      this.error.set('No authenticated user.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      await this.apiClient.todos.post({ userId, description: this.description, labels: [] });
      this.description = '';
      await this.reload();
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not create the todo.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async toggleComplete(todo: TodoResponse): Promise<void> {
    if (!todo.id || todo.isCompleted) {
      return;
    }

    this.error.set(null);

    try {
      await this.apiClient.todos.byId(todo.id).complete.put();
      await this.reload();
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not update the todo.'));
    }
  }

  protected async deleteTodo(todo: TodoResponse): Promise<void> {
    if (!todo.id) {
      return;
    }

    this.error.set(null);

    try {
      await this.apiClient.todos.byId(todo.id).delete();
      await this.reload();
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not delete the todo.'));
    }
  }

  private async reload(): Promise<void> {
    const userId = this.currentUser.userId();

    if (!userId) {
      this.error.set('No authenticated user.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);

    try {
      const result = await this.apiClient.todos.get({ queryParameters: { userId } });
      this.todos.set(result ?? []);
    } catch (error) {
      this.error.set(extractErrorMessage(error, 'Could not load todos.'));
    } finally {
      this.loading.set(false);
    }
  }
}
