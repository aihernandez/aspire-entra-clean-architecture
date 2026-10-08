# Regression checks for the replacement boundary. Tool output is simulated; test-clean-setup.ps1
# separately runs the real build and generator from a candidate snapshot.
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kiota-regression-' + [guid]::NewGuid())
$generator = Join-Path $fixtureRoot 'scripts/generate-api-client.ps1'
$shared = Join-Path $fixtureRoot 'clients/api-client/apiClient.ts'
$consumer = Join-Path $fixtureRoot 'src/frontend/src/app/api-client/apiClient.ts'
$global:kiotaRegressionState = @{ Failure = ''; FailMove = $false }

function dotnet {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq $global:kiotaRegressionState.Failure) { $global:LASTEXITCODE = 1; return }
    if ($args[0] -eq 'kiota') {
        $outputIndex = [array]::IndexOf($args, '--output')
        $output = $args[$outputIndex + 1]
        New-Item -ItemType Directory -Path $output -Force | Out-Null
        if ($global:kiotaRegressionState.Failure -ne 'invalid-output') {
            Set-Content (Join-Path $output 'apiClient.ts') 'new-contract'
        }
    }
}

function Move-Item {
    param([string]$LiteralPath, [string]$Destination)
    if ($global:kiotaRegressionState.FailMove -and (Split-Path -Leaf $LiteralPath) -eq 'staged-1') {
        $global:kiotaRegressionState.FailMove = $false
        throw 'Simulated replacement failure'
    }
    Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
}

function Assert-Content([string]$expected) {
    foreach ($path in @($shared, $consumer)) {
        if ((Get-Content -LiteralPath $path -Raw).Trim() -ne $expected) {
            throw "Unexpected client contents at $path"
        }
    }
}

function Assert-Failure([string]$expected) {
    $caught = $false
    try { & $generator }
    catch {
        if ($_.Exception.Message -notlike "*$expected*") { throw }
        $caught = $true
    }
    if (-not $caught) { throw "Expected failure: $expected" }
    Assert-Content 'valid-contract'
}

try {
    New-Item -ItemType Directory -Path (Split-Path -Parent $generator),
        (Join-Path $fixtureRoot 'src/backend/Web.Api/obj/openapi'),
        (Join-Path $fixtureRoot 'src/frontend/src/app') -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'generate-api-client.ps1') $generator
    Set-Content (Join-Path $fixtureRoot 'src/backend/Web.Api/obj/openapi/Web.Api.json') '{}'
    $originalLocation = (Get-Location).Path
    & $generator
    Assert-Content 'new-contract'
    & $generator
    Assert-Content 'new-contract'
    if ((Get-Location).Path -ne $originalLocation) { throw 'Generator changed caller location.' }

    foreach ($failure in @('tool', 'build', 'kiota', 'invalid-output')) {
        Set-Content $shared 'valid-contract'
        Set-Content $consumer 'valid-contract'
        $global:kiotaRegressionState.Failure = $failure
        $expected = if ($failure -eq 'invalid-output') { 'did not produce' } else { 'exit code 1' }
        Assert-Failure $expected
    }
    $global:kiotaRegressionState.Failure = ''
    $global:kiotaRegressionState.FailMove = $true
    Assert-Failure 'Simulated replacement failure'
    Write-Host 'PASS: absent parents, repeat, caller location, tool/build/generation failures and replacement rollback.'
}
finally {
    Remove-Variable kiotaRegressionState -Scope Global
    $resolved = [System.IO.Path]::GetFullPath($fixtureRoot)
    $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolved).StartsWith('kiota-regression-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
