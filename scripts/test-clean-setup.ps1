<#
.SYNOPSIS
    Verifies the current candidate files in an isolated snapshot without ignored build output.
.DESCRIPTION
    Includes tracked and non-ignored untracked files, using their current working-tree contents.
    Leaves the snapshot in the system temp directory for inspection; never deletes the checkout.
#>
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$snapshot = Join-Path ([System.IO.Path]::GetTempPath()) ('template-clean-' + [guid]::NewGuid())

$files = git -C $repoRoot ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate candidate files.' }

New-Item -ItemType Directory -Path $snapshot | Out-Null
Write-Host "Clean candidate snapshot: $snapshot"
foreach ($relative in ($files | Sort-Object -Unique)) {
    $source = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    $target = [System.IO.Path]::GetFullPath((Join-Path $snapshot $relative))
    if (-not $target.StartsWith($snapshot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Candidate path outside snapshot: $relative"
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}

if (Test-Path (Join-Path $snapshot 'clients')) { throw 'The snapshot unexpectedly contains clients/.' }

# UserSecretsId is shared with the source machine. Change it only in the isolated snapshot.
Get-ChildItem -LiteralPath $snapshot -Recurse -Filter *.csproj | ForEach-Object {
    $content = Get-Content -LiteralPath $_.FullName -Raw
    $isolated = [regex]::Replace($content, '<UserSecretsId>[^<]+</UserSecretsId>',
        '<UserSecretsId>clean-snapshot-' + [guid]::NewGuid() + '</UserSecretsId>')
    if ($content -ne $isolated) { Set-Content -LiteralPath $_.FullName -Value $isolated -NoNewline }
}

& (Join-Path $snapshot 'scripts/setup-dev.ps1')
Push-Location (Join-Path $snapshot 'src/frontend')
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Angular build failed in the clean snapshot.' }
}
finally { Pop-Location }

# Repeat from outside the repository to verify local-tool resolution and safe regeneration.
Push-Location ([System.IO.Path]::GetTempPath())
try { & (Join-Path $snapshot 'scripts/setup-dev.ps1') }
finally { Pop-Location }
Write-Host "Clean setup and repeat succeeded. Snapshot retained at $snapshot"
