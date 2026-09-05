namespace Infrastructure.Authentication;

/// <summary>
/// Hardening applied on top of what <c>Microsoft.Identity.Web</c> validates by default. Bound from
/// the <c>AzureAd</c> configuration section alongside the settings the library itself reads.
/// </summary>
internal sealed class EntraIdOptions
{
    public const string SectionName = "AzureAd";

    public string? Instance { get; set; }

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }

    /// <summary>
    /// When true (the default), tokens are accepted only from <see cref="TenantId"/>. Microsoft's
    /// guidance is blunt about this: <i>"Never allow data in one tenant to be accessed from another
    /// tenant."</i> Turn it off only for a deliberately multi-tenant deployment, and only together
    /// with per-tenant data scoping.
    /// </summary>
    // PROVISIONAL(2026-09-04): poner esto en false habilita tokens de cualquier tenant, pero NINGUNA
    // consulta filtra por tenant todavía — IUserContext.TenantId existe y no lo usa nadie. Hoy la
    // combinación segura es la única soportada (single-tenant, ValidateTenant = true). Al cerrarlo
    // (scoping por EntraTenantId en las queries) el template soporta multi-tenant de verdad; hasta
    // entonces, apagar esta bandera filtra datos entre organizaciones.
    public bool ValidateTenant { get; set; } = true;
}
