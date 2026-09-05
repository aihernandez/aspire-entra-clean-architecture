using SharedKernel;

namespace Domain.Users;

/// <summary>
/// The application's local record of a person. It is deliberately <b>not</b> an identity store:
/// it holds no credentials and no roles. Entra ID owns authentication, and roles arrive in the
/// access token's <c>roles</c> claim on every request (see EntraIdMigration.md, D3/D4).
/// <para>
/// What lives here is a display cache — name and email copied from token claims so the app can
/// render "assigned to ..." and join on a real foreign key without calling Microsoft Graph — plus
/// application-owned state such as <see cref="IsActive"/>.
/// </para>
/// </summary>
public sealed class User : Entity
{
    /// <summary>Local, application-owned primary key. Stable across tenant changes.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>oid</c> claim: immutable per user, per tenant. Unique together with
    /// <see cref="EntraTenantId"/> — never on its own, because the same person has a different
    /// <c>oid</c> in every tenant they belong to.
    /// </summary>
    public Guid EntraObjectId { get; set; }

    /// <summary>The <c>tid</c> claim. Half of the identity key.</summary>
    public Guid EntraTenantId { get; set; }

    /// <summary>Display cache, refreshed from token claims. Never used for authorization.</summary>
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// False once the person is deprovisioned. Rows are switched off, never deleted, so that a
    /// SCIM endpoint can be added later without reshaping the model — and so foreign keys from
    /// their historical data keep resolving.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime? DeactivatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }
}
