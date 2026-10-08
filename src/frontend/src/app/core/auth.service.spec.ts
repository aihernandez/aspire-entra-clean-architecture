import { afterEach, beforeEach, describe, expect, it, type Mock, vi } from "vitest";
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

/**
 * The development-bypass path, which is what a freshly cloned template runs on. It matters that
 * this is covered: if `initialize()` ever failed to recognise an unconfigured API, the app would
 * try to redirect to a tenant that does not exist and show a blank page instead of an error.
 *
 * The MSAL path is deliberately not unit-tested here — it redirects the browser to Microsoft, so
 * asserting on it would only test a mock of MSAL. Validate it separately with a live tenant.
 */
describe('AuthService', () => {
    let service: AuthService;
    let fetchSpy: Mock<typeof fetch>;

    beforeEach(() => {
        TestBed.configureTestingModule({});
        service = TestBed.inject(AuthService);
        fetchSpy = vi.fn<typeof fetch>();
        vi.stubGlobal('fetch', fetchSpy);
    });

    afterEach(() => vi.unstubAllGlobals());

    function respondWith(body: unknown, ok = true): void {
        fetchSpy.mockResolvedValue({ ok, json: () => Promise.resolve(body) } as Response);
    }

    it('treats a disabled auth-config as development bypass', async () => {
        respondWith({ enabled: false, clientId: '', authority: '', apiScope: '' });

        await service.initialize();

        expect(service.isReady()).toBe(true);
        expect(service.isAuthenticated()).toBe(true);
        expect(service.error()).toBeNull();
        expect(await service.getAccessToken()).toBeNull();
    });

    it('shows an error when the API is unreachable and never grants a bypass', async () => {
        fetchSpy.mockRejectedValue(new Error('connection refused'));

        await service.initialize();

        expect(service.isReady()).toBe(true);
        expect(service.isAuthenticated()).toBe(false);
        expect(service.error()).toContain('Could not load sign-in settings');
    });

    it('reports the stand-in identity while bypassing', async () => {
        respondWith({ enabled: false, clientId: '', authority: '', apiScope: '' });

        await service.initialize();

        expect(service.email()).toBe('dev.user@localhost');
        expect(service.error()).toBeNull();
    });

    it('sends no Authorization header while bypassing', async () => {
        // The API's development scheme authenticates by configuration, so attaching a token would be
        // meaningless — and attaching an empty one would look like a malformed request.
        respondWith({ enabled: false, clientId: '', authority: '', apiScope: '' });

        await service.initialize();

        expect(await service.getAccessToken()).toBeNull();
    });

    it('does not sign out or sign in while bypassing', async () => {
        respondWith({ enabled: false, clientId: '', authority: '', apiScope: '' });
        await service.initialize();

        // Both are no-ops rather than throwing: the shell renders a sign-out button regardless.
        await expect(service.signIn()).resolves.toBeUndefined();
        await expect(service.signOut()).resolves.toBeUndefined();
    });
});
