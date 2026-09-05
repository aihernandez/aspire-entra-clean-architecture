namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    /// <summary>
    /// Mirrors <c>DevelopmentAuthenticationHandler</c>'s header names. Real Entra ID tokens cannot
    /// be obtained in CI — there is no tenant, no interactive sign-in and no secret to hand out —
    /// so the suite drives the development authentication scheme instead and varies the identity
    /// per request through these headers.
    /// </summary>
    private const string AnonymousHeader = "X-Dev-Anonymous";
    private const string ObjectIdHeader = "X-Dev-ObjectId";
    private const string RolesHeader = "X-Dev-Roles";
    private const string EmailHeader = "X-Dev-Email";
    private const string NoRoles = "none";

    protected const string AdminRole = "Admin";
    protected const string MemberRole = "Member";

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
    }

    protected IntegrationTestWebAppFactory Factory { get; }

    protected HttpClient HttpClient { get; }

    /// <summary>
    /// A client authenticated as a distinct person. Each call invents a new <c>oid</c>, so the
    /// first request it makes exercises just-in-time provisioning for real.
    /// </summary>
    protected HttpClient CreateClientAs(Guid objectId, params string[] roles)
    {
        HttpClient client = Factory.CreateClient();

        client.DefaultRequestHeaders.Add(ObjectIdHeader, objectId.ToString());
        client.DefaultRequestHeaders.Add(EmailHeader, $"user-{objectId:N}@example.com");
        // An empty header value is not reliably transmitted, so "assigned to the tenant but not to
        // this application" is expressed with an explicit sentinel rather than an empty string.
        client.DefaultRequestHeaders.Add(RolesHeader, roles.Length > 0 ? string.Join(',', roles) : NoRoles);

        return client;
    }

    protected HttpClient CreateMemberClient(out Guid objectId)
    {
        objectId = Guid.NewGuid();

        return CreateClientAs(objectId, MemberRole);
    }

    protected HttpClient CreateAdminClient() => CreateClientAs(Guid.NewGuid(), AdminRole, MemberRole);

    /// <summary>
    /// Somebody who authenticated against the tenant but holds no App Role for this application.
    /// Every endpoint must refuse them — that is the whole point of requiring a permission rather
    /// than merely a valid token.
    /// </summary>
    protected HttpClient CreateUnassignedClient() => CreateClientAs(Guid.NewGuid());

    protected HttpClient CreateAnonymousClient()
    {
        HttpClient client = Factory.CreateClient();

        client.DefaultRequestHeaders.Add(AnonymousHeader, "true");

        return client;
    }
}
