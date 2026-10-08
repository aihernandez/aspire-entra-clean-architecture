import { beforeEach, describe, expect, it, type Mock, vi } from "vitest";
import { TestBed } from '@angular/core/testing';
import { ApiClientService } from './api-client.provider';
import { AuthService } from './auth.service';
import { CurrentUserService } from './current-user.service';

describe('CurrentUserService', () => {
    let service: CurrentUserService;
    let getCurrent: Mock;

    beforeEach(() => {
        getCurrent = vi.fn().mockName('get current user');
        TestBed.configureTestingModule({
            providers: [
                { provide: ApiClientService, useValue: { client: { users: { me: { get: getCurrent } } } } },
                { provide: AuthService, useValue: { email: () => null, name: () => null } }
            ]
        });
        service = TestBed.inject(CurrentUserService);
    });

    it('shares a pending request and uses API permissions for the menu', async () => {
        let resolve!: (value: object) => void;
        getCurrent.mockReturnValue(new Promise<object>(r => { resolve = r; }));

        const first = service.load();
        const second = service.load();
        expect(getCurrent).toHaveBeenCalledTimes(1);
        expect(first).toBe(second);
        expect(service.canReadUsers()).toBe(false);

        resolve({ id: 'user-1', email: 'admin@example.com', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'], permissions: ['users:read-all'] });
        await Promise.all([first, second]);

        expect(service.roles()).toEqual(['Admin']);
        expect(service.canReadUsers()).toBe(true);
        await service.load();
        expect(getCurrent).toHaveBeenCalledTimes(1);
    });

    it('retries after a failed request without granting permissions', async () => {
        getCurrent.mockRejectedValue(new Error('offline'));
        await expect(service.load()).rejects.toThrow('offline');
        expect(service.canReadUsers()).toBe(false);

        getCurrent.mockResolvedValue({ id: 'member-1', roles: ['Member'], permissions: ['todos:access'] });
        await expect(service.load()).resolves.toMatchObject({ id: 'member-1', roles: ['Member'] });
        expect(getCurrent).toHaveBeenCalledTimes(2);
        expect(service.roles()).toEqual(['Member']);
        expect(service.canReadUsers()).toBe(false);
    });
});
