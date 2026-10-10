import { describe, expect, it, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { ApiClientService } from '../core/api-client.provider';
import { CurrentUserService } from '../core/current-user.service';
import { UserDetailComponent } from './user-detail.component';

describe('UserDetailComponent', () => {
  it('saves a local access change through the typed client', async () => {
    const put = vi.fn().mockResolvedValue(undefined);
    const get = vi.fn().mockResolvedValue({
      id: 'target-1', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', isActive: true
    });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'target-1' } } } },
        { provide: CurrentUserService, useValue: { userId: () => 'admin-1', canManageUsers: () => true } },
        { provide: ApiClientService, useValue: { client: { users: { byUserId: () => ({ get, status: { put } }) } } } }
      ]
    });

    const fixture = TestBed.createComponent(UserDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const checkbox = fixture.nativeElement.querySelector('input[type="checkbox"]') as HTMLInputElement;
    checkbox.click();
    fixture.detectChanges();
    const save = [...fixture.nativeElement.querySelectorAll('button')]
      .find((button: HTMLButtonElement) => button.textContent?.trim() === 'Save changes') as HTMLButtonElement;
    save.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(put).toHaveBeenCalledWith({ isActive: false });
    expect(fixture.nativeElement.textContent).toContain('Access updated.');
  });
});
