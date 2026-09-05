<#
.SYNOPSIS
    Regenerates the Kiota-based TypeScript API client from the current Web.Api OpenAPI document,
    and syncs it into every frontend that consumes it.

.DESCRIPTION
    1. Builds Web.Api, which emits src/backend/Web.Api/obj/openapi/Web.Api.json via
       Microsoft.Extensions.ApiDescription.Server (see Web.Api.csproj).
    2. Runs `dotnet kiota generate` (installed as a local tool, see .config/dotnet-tools.json)
       against that document, producing a framework-agnostic TypeScript client in
       clients/api-client/. That folder is generated output — it's gitignored — and is the single
       source of truth, meant to be shared by every frontend instead of generating it once per
       framework.
    3. Copies that output into each frontend's consumed copy (currently just
       src/frontend/src/app/api-client/ for Angular). Angular can't import straight from
       clients/api-client/ without a build-time path mapping, so this copy step is what keeps it
       in sync — skipping it (e.g. by calling `dotnet kiota generate` directly instead of this
       script) silently leaves the frontend on a stale client. Add a new line here for any future
       frontend (e.g. a React app) that needs the same client.

.EXAMPLE
    ./scripts/generate-api-client.ps1
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$webApiProject = Join-Path $repoRoot "src/backend/Web.Api/Web.Api.csproj"
$openApiDocument = Join-Path $repoRoot "src/backend/Web.Api/obj/openapi/Web.Api.json"
$outputDir = Join-Path $repoRoot "clients/api-client"

# Every frontend that consumes the generated client — add a new entry here for a future frontend
# (e.g. src/frontend-react/src/api-client).
$consumers = @(
    Join-Path $repoRoot "src/frontend/src/app/api-client"
)

Write-Host "Restoring local dotnet tools (kiota)..." -ForegroundColor Cyan
dotnet tool restore --tool-manifest (Join-Path $repoRoot ".config/dotnet-tools.json")

Write-Host "Building Web.Api to regenerate the OpenAPI document..." -ForegroundColor Cyan
dotnet build $webApiProject

if (-not (Test-Path $openApiDocument)) {
    throw "OpenAPI document not found at $openApiDocument after build."
}

if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}

Write-Host "Generating the TypeScript API client with Kiota..." -ForegroundColor Cyan
dotnet kiota generate `
    --language typescript `
    --openapi $openApiDocument `
    --class-name ApiClient `
    --namespace-name api-client `
    --output $outputDir

Write-Host "Done. Client written to $outputDir" -ForegroundColor Green

foreach ($consumer in $consumers) {
    if (-not (Test-Path (Split-Path -Parent $consumer))) {
        Write-Host "Skipping sync to $consumer (parent folder doesn't exist)." -ForegroundColor DarkYellow
        continue
    }

    Write-Host "Syncing client to $consumer..." -ForegroundColor Cyan

    if (Test-Path $consumer) {
        Remove-Item -Recurse -Force $consumer
    }

    Copy-Item -Recurse -Force $outputDir $consumer
}

Write-Host "Done. Client synced to: $($consumers -join ', ')" -ForegroundColor Green
