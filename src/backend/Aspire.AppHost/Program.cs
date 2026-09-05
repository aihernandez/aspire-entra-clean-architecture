using CommunityToolkit.Aspire.Hosting.MailPit;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Fixed password for local development convenience, mirroring how the Postgres flavor
// of this template ships with a fixed "postgres"/"postgres" credential. Override via
// user-secrets (Parameters:sql-password) for anything beyond local dev.
IResourceBuilder<ParameterResource> sqlPassword = builder.AddParameter(
    "sql-password",
    "yourStrong(!)Password123",
    secret: true);

IResourceBuilder<SqlServerDatabaseResource> database = builder
    .AddSqlServer("sql")
    .WithImage("mssql/server", "2025-latest")
    .WithPassword(sqlPassword)
    // A Docker-managed named volume, not a host bind mount: SQL Server 2025's Linux image runs a
    // Windows-compatibility layer (SQLPAL) that fails to start ("Failed to load LSA: 0xc0070102")
    // when its data directory is bind-mounted onto a Windows host path via Docker Desktop.
    .WithDataVolume()
    .AddDatabase("clean-architecture");

// A local SMTP catcher with a web UI (http://localhost:8025 by default) — Web.Api sends real
// SMTP traffic to it via MailKit, so the email pipeline runs for real in local dev without a
// live mail provider. Web.Api reads its "mailpit" connection string (endpoint=smtp://host:port)
// and falls back to explicit Smtp:* config (a real provider) when that connection string is
// absent, e.g. in Production — see Infrastructure/DependencyInjection.cs's AddEmail.
IResourceBuilder<MailPitContainerResource> mailpit = builder.AddMailPit("mailpit");

IResourceBuilder<ProjectResource> webApi = builder.AddProject<Projects.Web_Api>("web-api")
    .WithEnvironment("ConnectionStrings__Database", database)
    .WithReference(database)
    .WaitFor(database)
    .WithReference(mailpit)
    .WaitFor(mailpit);

// The Angular dev server runs as a plain npm script ("start" -> `ng serve`). Browsers can't use
// Aspire's server-side service discovery, so the API base URL is a fixed constant on the Angular
// side (src/frontend/src/app/core/api-config.ts) matching Web.Api's "http" launch profile port.
builder.AddJavaScriptApp("frontend-angular", "../../frontend", "start")
    .WithNpm()
    .WithHttpEndpoint(port: 4200, targetPort: 4200, env: "PORT", isProxied: false)
    .WithReference(webApi)
    .WaitFor(webApi);

builder.Build().Run();
