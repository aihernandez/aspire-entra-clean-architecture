$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $repoRoot "src/frontend"

foreach ($command in @("dotnet", "node", "npm", "docker")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required for local development."
    }
}

docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Docker must be running for SQL Server and MailPit."
}

Push-Location $frontend
try {
    npm ci
    if ($LASTEXITCODE -ne 0) {
        throw "npm ci failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

& (Join-Path $PSScriptRoot "generate-api-client.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "API client generation failed with exit code $LASTEXITCODE."
}

Write-Host "Ready. Run: dotnet run --project src/backend/Aspire.AppHost" -ForegroundColor Green
