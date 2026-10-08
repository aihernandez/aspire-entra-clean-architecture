import { describe, expect, it } from "vitest";
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { CurrentUserService } from '../core/current-user.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
    it('shows Users only when the API profile grants users:read-all', async () => {
        const allowed = signal(false);
        TestBed.configureTestingModule({
            providers: [
                provideRouter([]),
                { provide: AuthService, useValue: { signOut: () => Promise.resolve() } },
                {
                    provide: CurrentUserService,
                    useValue: {
                        load: () => Promise.resolve({ id: 'user-1' }),
                        canReadUsers: () => allowed(),
                        email: () => 'member@example.com'
                    }
                }
            ]
        });

        const fixture = TestBed.createComponent(ShellComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        expect(fixture.nativeElement.querySelector('a[href="/users"]')).toBeNull();

        allowed.set(true);
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelector('a[href="/users"]')).not.toBeNull();
    });
});
