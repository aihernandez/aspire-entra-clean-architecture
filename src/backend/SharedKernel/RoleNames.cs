namespace SharedKernel;

/// <summary>
/// Entra ID <b>App Role</b> values, as declared in the API's app registration manifest and
/// received in the access token's <c>roles</c> claim.
/// <para>
/// These are deliberately few and coarse. Fine-grained authorization is expressed with
/// <see cref="PermissionNames"/>, which this application maps from roles in code — modelling every
/// permission as its own App Role would inflate every token and turn each new permission into a
/// configuration change in the customer's tenant, performed by an administrator we don't control
/// (see EntraIdMigration.md, D5).
/// </para>
/// <para>
/// The string values must match the <c>value</c> of the corresponding App Role in the manifest
/// exactly; they are a contract with the directory, not an internal name.
/// </para>
/// </summary>
// DUDA(2026-09-04): ¿el tenant destino tiene licencia Entra ID P1/P2? Sin ella no se puede asignar
// un App Role a un grupo de seguridad, y la asignación pasa a ser usuario por usuario en la
// Enterprise Application. No cambia este código, pero sí lo que se le promete al cliente sobre cómo
// administra los accesos su equipo de IT. Ver EntraIdSetup.md, paso 4.
public static class RoleNames
{
    public const string Admin = "Admin";

    /// <summary>
    /// The baseline role every person assigned to the application receives. Its existence is what
    /// makes "signed in" and "authorized to use this app" different statements — a token can be
    /// valid for the tenant without its subject being a user of this application at all.
    /// </summary>
    public const string Member = "Member";
}
