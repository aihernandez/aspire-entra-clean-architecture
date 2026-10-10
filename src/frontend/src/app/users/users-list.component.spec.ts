import { describe, expect, it, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ApiClientService } from '../core/api-client.provider';
import { CurrentUserService } from '../core/current-user.service';
import { UsersListComponent } from './users-list.component';

describe('UsersListComponent', () => {
  it('requests the next page and keeps inactive users visible for administration', async () => {
    const get = vi.fn()
      .mockResolvedValueOnce({
        items: [{ id: 'user-1', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', isActive: false }],
        totalCount: { value: 21 }
      })
      .mockResolvedValueOnce({ items: [], totalCount: { value: 21 } });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CurrentUserService, useValue: { userId: () => 'admin-1' } },
        { provide: ApiClientService, useValue: { client: { users: { get } } } }
      ]
    });

    const fixture = TestBed.createComponent(UsersListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(get).toHaveBeenCalledWith({
      queryParameters: { pageNumber: '1', pageSize: '20', includeInactive: true }
    });
    expect(fixture.nativeElement.textContent).toContain('Deactivated');

    const next = [...fixture.nativeElement.querySelectorAll('button')]
      .find((button: HTMLButtonElement) => button.textContent?.trim() === 'Next') as HTMLButtonElement;
    next.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(get).toHaveBeenLastCalledWith({
      queryParameters: { pageNumber: '2', pageSize: '20', includeInactive: true }
    });
    expect(fixture.nativeElement.textContent).toContain('Page 2 of 2');
  });

  it('requires confirmation before deactivating a user', async () => {
    const remove = vi.fn().mockResolvedValue(undefined);
    const get = vi.fn().mockResolvedValue({
      items: [{ id: 'user-1', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', isActive: true }],
      totalCount: { value: 1 }
    });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CurrentUserService, useValue: { userId: () => 'admin-1' } },
        { provide: ApiClientService, useValue: { client: { users: { get, byUserId: () => ({ delete: remove }) } } } }
      ]
    });

    const fixture = TestBed.createComponent(UsersListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const button = (label: string): HTMLButtonElement => [...fixture.nativeElement.querySelectorAll('button')]
      .find((element: HTMLButtonElement) => element.textContent?.trim() === label) as HTMLButtonElement;

    button('Delete').click();
    fixture.detectChanges();
    expect(remove).not.toHaveBeenCalled();
    button('Confirm').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(remove).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Deactivated');
  });
});
