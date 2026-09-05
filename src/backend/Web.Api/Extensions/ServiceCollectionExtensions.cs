using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Web.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

            // Endpoint request DTOs are nested `Request` records (one per endpoint class), so the
            // default schema-id (the bare type name) collides across endpoints and silently merges
            // unrelated request bodies into a single "Request" schema. Prefix nested types with
            // their declaring type's name to keep them distinct, mirroring what CustomSchemaIds did
            // for Swashbuckle before this template moved to the native OpenApi generator.
            options.CreateSchemaReferenceId = type =>
            {
                Type clrType = type.Type;

                return clrType is { IsNested: true, DeclaringType: not null }
                    ? $"{clrType.DeclaringType.Name}{clrType.Name}"
                    : OpenApiOptions.CreateDefaultSchemaReferenceId(type);
            };
        });

        return services;
    }

    private sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
        : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            IEnumerable<AuthenticationScheme> authenticationSchemes =
                await authenticationSchemeProvider.GetAllSchemesAsync();

            if (!authenticationSchemes.Any())
            {
                return;
            }

            // Deliberately not keyed on a scheme literally named "Bearer". This host registers
            // either the Entra ID scheme (which is called "Bearer") or the Development stand-in
            // (which is not), so matching on the name emitted a documented security scheme in one
            // environment and silently none in the other — leaving Scalar with no place to paste a
            // token exactly when someone points a locally-running app at a real tenant.
            //
            // The wire format is "Authorization: Bearer <token>" either way, so that is what the
            // document describes.
            var securityScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste an Entra ID access token. Local development needs none: the " +
                              "development authentication scheme authenticates every request by " +
                              "configuration."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                [JwtBearerDefaults.AuthenticationScheme] = securityScheme
            };

            foreach (IOpenApiPathItem pathItem in document.Paths.Values)
            {
                foreach (OpenApiOperation operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
                    });
                }
            }
        }
    }
}
