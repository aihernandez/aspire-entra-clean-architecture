namespace Application.Abstractions.Authentication;

/// <summary>
/// The authenticated caller, as seen by the Application layer. Populated from the access token's
/// claims plus the just-in-time provisioned local record — never from a database lookup performed
/// for authorization purposes.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// The <b>local</b> primary key of the caller's <c>User</c> row, not the directory's
    /// <c>oid</c>. Foreign keys elsewhere in the model point at this value.
    /// </summary>
    Guid UserId { get; }

    /// <summary>The <c>tid</c> claim — the tenant whose data this request may touch.</summary>
    Guid TenantId { get; }

    /// <summary>
    /// The App Roles carried by the access token's <c>roles</c> claim. The single source of truth
    /// for what the caller may do; the local user record has no say in it.
    /// </summary>
    IReadOnlyList<string> Roles { get; }
}
