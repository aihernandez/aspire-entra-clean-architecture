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

function Assert-NativeSuccess([string]$step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$step failed with exit code $LASTEXITCODE. Existing generated clients were preserved."
    }
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("kiota-" + [guid]::NewGuid())
$generated = Join-Path $temporaryRoot "generated"
$destinations = @($outputDir) + $consumers
$workspaceRoot = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$preserveRecoveryFiles = $false

foreach ($destination in $destinations) {
    $fullPath = [System.IO.Path]::GetFullPath($destination)
    if (-not $fullPath.StartsWith($workspaceRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace a client outside the workspace: $fullPath"
    }
}
if (-not [System.IO.Path]::GetFullPath($temporaryRoot).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a temporary directory outside the system temp root."
}

Push-Location $repoRoot
try {
    Write-Host "Restoring local dotnet tools (kiota)..." -ForegroundColor Cyan
    dotnet tool restore --tool-manifest (Join-Path $repoRoot ".config/dotnet-tools.json")
    Assert-NativeSuccess "dotnet tool restore"

    Write-Host "Building Web.Api to regenerate the OpenAPI document..." -ForegroundColor Cyan
    dotnet build $webApiProject
    Assert-NativeSuccess "dotnet build"

    if (-not (Test-Path $openApiDocument)) {
        throw "OpenAPI document not found at $openApiDocument after build."
    }

    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    Write-Host "Generating the TypeScript API client with Kiota..." -ForegroundColor Cyan
    dotnet kiota generate `
        --language typescript `
        --openapi $openApiDocument `
        --class-name ApiClient `
        --namespace-name api-client `
        --output $generated
    Assert-NativeSuccess "dotnet kiota generate"

    if (-not (Test-Path (Join-Path $generated "apiClient.ts"))) {
        throw "Kiota did not produce apiClient.ts. Existing generated clients were preserved."
    }

    # Stage every copy before replacing any consumer. If a replacement fails, restore all copies.
    for ($index = 0; $index -lt $destinations.Count; $index++) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $destinations[$index]) -Force | Out-Null
        Copy-Item -Recurse -Path $generated -Destination (Join-Path $temporaryRoot "staged-$index")
        if (Test-Path $destinations[$index]) {
            Copy-Item -Recurse -Path $destinations[$index] -Destination (Join-Path $temporaryRoot "backup-$index")
        }
    }

    $attempted = [System.Collections.Generic.List[int]]::new()
    try {
        for ($index = 0; $index -lt $destinations.Count; $index++) {
            $destination = $destinations[$index]
            $attempted.Add($index)
            if (Test-Path $destination) {
                Remove-Item -Recurse -Force -LiteralPath $destination
            }
            Move-Item -LiteralPath (Join-Path $temporaryRoot "staged-$index") -Destination $destination
        }
    }
    catch {
        $replacementError = $_
        foreach ($index in $attempted) {
            try {
                $destination = $destinations[$index]
                if (Test-Path $destination) {
                    Remove-Item -Recurse -Force -LiteralPath $destination
                }
                $backup = Join-Path $temporaryRoot "backup-$index"
                if (Test-Path $backup) {
                    Move-Item -LiteralPath $backup -Destination $destination
                }
            }
            catch {
                $preserveRecoveryFiles = $true
                Write-Warning "Recovery failed for $($destinations[$index]). Backups retained at $temporaryRoot."
            }
        }
        throw $replacementError
    }

    Write-Host "Client synced to: $($destinations -join ', ')" -ForegroundColor Green
}
finally {
    Pop-Location
    if (-not $preserveRecoveryFiles -and (Test-Path $temporaryRoot)) {
        Remove-Item -Recurse -Force -LiteralPath $temporaryRoot
    }
}
