using System.Data.Common;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Email;
using Infrastructure.Email.Templates;
using Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        services
            .AddServices()
            .AddDatabase(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration, environment)
            .AddAuthorizationInternal()
            .AddEmail(configuration);

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

#pragma warning disable EXTEXP0018 // HybridCache is released; the API is stable in .NET 10.
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        services.AddDbContext<ApplicationDbContext>(
            options => options
                .UseSqlServer(connectionString, sqlServerOptions =>
                    sqlServerOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddSqlServer(configuration.GetConnectionString("Database")!);

        return services;
    }

    /// <summary>
    /// Registers exactly one of two authentication schemes: Entra ID when the application is
    /// configured against a tenant, or the development stand-in when it isn't and the host
    /// environment is Development.
    /// </summary>
    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        IConfigurationSection developmentSection = configuration.GetSection(DevelopmentAuthenticationOptions.SectionName);

        // The development scheme authenticates every request without a token. Shipping it enabled
        // would be a silent authentication bypass, so its mere presence outside Development is
        // treated as a deployment error rather than something to warn about and carry on.
        if (developmentSection.Exists() && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"'{DevelopmentAuthenticationOptions.SectionName}' is configured in the " +
                $"'{environment.EnvironmentName}' environment. That section bypasses authentication " +
                "and is only permitted in Development. Remove it from this environment's configuration.");
        }

        var entraIdOptions = new EntraIdOptions();
        configuration.GetSection(EntraIdOptions.SectionName).Bind(entraIdOptions);

        // Development with no tenant configured is the "just cloned the template" path. Everywhere
        // else the Entra scheme is registered unconditionally, even when the configuration is
        // incomplete: an unconfigured scheme rejects every token, which is the safe failure, and
        // throwing here would break `dotnet build` — the OpenAPI document generator boots this very
        // Program with no ASPNETCORE_ENVIRONMENT set, so it runs as Production.
        bool useDevelopmentAuthentication =
            environment.IsDevelopment() && string.IsNullOrWhiteSpace(entraIdOptions.ClientId);

        if (useDevelopmentAuthentication)
        {
            AddDevelopmentAuthentication(services, developmentSection);
        }
        else
        {
            AddEntraId(services, configuration, entraIdOptions);
        }

        string scheme = useDevelopmentAuthentication
            ? AuthenticationSchemes.Development
            : AuthenticationSchemes.EntraId;

        services.AddSingleton<IAuthenticationSchemeRegistry>(new AuthenticationSchemeRegistry([scheme]));

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }

    private static void AddEntraId(
        IServiceCollection services,
        IConfiguration configuration,
        EntraIdOptions entraIdOptions)
    {
        services
            .AddAuthentication(AuthenticationSchemes.EntraId)
            .AddMicrosoftIdentityWebApi(
                configuration.GetSection(EntraIdOptions.SectionName),
                AuthenticationSchemes.EntraId);

        // TODO(2026-09-04): habilitar Continuous Access Evaluation — al cerrarlo, una cuenta
        // deshabilitada en Entra pierde acceso en minutos en vez de esperar a que expire su token,
        // que es la garantía de baja de personal que pide una app interna. Requiere responder al
        // claims challenge con un 401 + WWW-Authenticate en vez de un 401 plano, y probarlo contra
        // un tenant real. Ver EntraIdMigration.md, preguntas abiertas.
        services.Configure<JwtBearerOptions>(AuthenticationSchemes.EntraId, options =>
        {
            // Microsoft.Identity.Web already validates the audience and the issuer. The tenant check
            // is added explicitly because "a valid token from some Entra tenant" and "a token from
            // OUR tenant" are different statements, and only the second one is what a single-tenant
            // internal application means to accept.
            options.TokenValidationParameters.ValidateIssuer = true;

            options.Events ??= new JwtBearerEvents();

            Func<TokenValidatedContext, Task>? inner = options.Events.OnTokenValidated;

            options.Events.OnTokenValidated = async context =>
            {
                if (inner is not null)
                {
                    await inner(context);
                }

                if (!entraIdOptions.ValidateTenant)
                {
                    return;
                }

                string? tenantId = context.Principal?.GetTenantId();

                if (!string.Equals(tenantId, entraIdOptions.TenantId, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("The token was issued by a tenant this application does not serve.");
                }
            };
        });
    }

    private static void AddDevelopmentAuthentication(IServiceCollection services, IConfigurationSection section)
    {
        services
            .AddAuthentication(AuthenticationSchemes.Development)
            .AddScheme<DevelopmentAuthenticationOptions, DevelopmentAuthenticationHandler>(
                AuthenticationSchemes.Development,
                options => section.Bind(options));
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }

    private static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailBrandingOptions>(configuration.GetSection(EmailBrandingOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        services.AddScoped(typeof(IEmailService<>), typeof(EmailService<>));
        services.AddScoped<IEmailBodyRenderer<WelcomeEmailModel>,
            RazorEmailBodyRenderer<WelcomeEmail, WelcomeEmailModel>>();
        services.AddScoped<IEmailTemplate<WelcomeEmailModel>, WelcomeEmailTemplate>();

        // Local dev: Aspire wires up a "mailpit" connection string (endpoint=smtp://host:port)
        // pointing at the MailPit container — see Aspire.AppHost/Program.cs. When present, it
        // takes priority over explicit Smtp:* config so `dotnet run` via the AppHost sends real
        // SMTP traffic to MailPit with zero manual configuration.
        string? mailpitConnectionString = configuration.GetConnectionString("mailpit");

        if (!string.IsNullOrWhiteSpace(mailpitConnectionString))
        {
            var connectionBuilder = new DbConnectionStringBuilder { ConnectionString = mailpitConnectionString };
            var endpoint = new Uri(connectionBuilder["Endpoint"].ToString()!, UriKind.Absolute);

            services.Configure<EmailOptions>(o =>
            {
                o.Host = endpoint.Host;
                o.Port = endpoint.Port;
                o.UseSsl = false;
            });

            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else if (!string.IsNullOrWhiteSpace(configuration[$"{EmailOptions.SectionName}:Host"]))
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
