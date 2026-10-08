import { describe, expect, it } from "vitest";
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CurrentUserService } from '../core/current-user.service';
import { ProfilePageComponent } from './profile-page.component';

describe('ProfilePageComponent', () => {
    it('renders the roles returned by the current-user API', async () => {
        const roles = signal<string[]>(['Member']);
        TestBed.configureTestingModule({
            providers: [{
                    provide: CurrentUserService,
                    useValue: {
                        load: () => Promise.resolve({ id: 'user-1' }),
                        displayName: () => 'Ada Lovelace',
                        email: () => 'ada@example.com',
                        userId: () => 'user-1',
                        roles: () => roles()
                    }
                }]
        });

        const fixture = TestBed.createComponent(ProfilePageComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        expect(fixture.nativeElement.textContent).toContain('Member');

        roles.set(['Admin']);
        fixture.detectChanges();
        expect(fixture.nativeElement.textContent).toContain('Admin');
        expect(fixture.nativeElement.textContent).not.toContain('Member');
    });
});
