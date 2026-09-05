<#
.SYNOPSIS
    Creates the two Microsoft Entra ID app registrations this template needs, and wires them into
    local configuration.

.DESCRIPTION
    Doing this by hand in the Azure portal is roughly fifteen steps across four blades, and the
    mistakes it invites are quiet ones: an app role whose value does not match the code, a redirect
    URI registered under the wrong platform, roles assigned on the client registration instead of
    the API's. This script performs the same steps through Microsoft Graph, in an order that cannot
    get them wrong.

    What it creates in your directory (nothing in your subscription, nothing billable):

      1. An API app registration  - exposes the 'access_as_user' scope and declares the
                                    'Member' and 'Admin' app roles.
      2. A SPA app registration   - single-page-application redirect URI, with delegated
                                    permission to the API's scope.
      3. A service principal for each (these are the "Enterprise applications" in the portal).
      4. 'Assignment required' switched on for the API, so only assigned people can obtain a token.
      5. Admin consent for the SPA to call the API.
      6. Both app roles assigned to you, so you can sign in immediately.

    Finally it writes AzureAd:TenantId / ClientId / SpaClientId into .NET user secrets, which live
    in your user profile and never enter the repository. The Angular app needs no configuration at
    all: it asks the API via GET /auth-config.

.PARAMETER NamePrefix
    Display-name prefix for both registrations. Defaults to "Aspire Entra Template".

.PARAMETER RedirectUri
    SPA redirect URI. Defaults to http://localhost:4200 (the Angular dev server).

.PARAMETER Force
    Create new registrations even if ones with these names already exist.

.EXAMPLE
    ./scripts/setup-entra.ps1

.EXAMPLE
    ./scripts/setup-entra.ps1 -NamePrefix "Contoso Ops" -RedirectUri "https://ops.contoso.com"

.NOTES
    Requires the Azure CLI, signed in to the target directory (az login).
    You need permission to create app registrations and to assign app roles — Application
    Developer plus Cloud Application Administrator, or any role that includes both.

    To undo everything:
        az ad app delete --id <API app id>
        az ad app delete --id <SPA app id>
#>

[CmdletBinding()]
param(
    [string]$NamePrefix = 'Aspire Entra Template',
    [string]$RedirectUri = 'http://localhost:4200',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$webApiProject = Join-Path $repoRoot 'src/backend/Web.Api'

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Write-Ok($message) { Write-Host "    $message" -ForegroundColor Green }
function Write-Note($message) { Write-Host "    $message" -ForegroundColor DarkGray }

function Invoke-Graph {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        $Body
    )

    $arguments = @('rest', '--method', $Method, '--url', $Url, '-o', 'json')

    if ($null -ne $Body) {
        $tempFile = [System.IO.Path]::GetTempFileName()
        ($Body | ConvertTo-Json -Depth 12 -Compress) | Out-File -FilePath $tempFile -Encoding utf8 -NoNewline
        $arguments += @('--headers', 'Content-Type=application/json', '--body', "@$tempFile")
    }

    try {
        $result = & az @arguments
        if ($LASTEXITCODE -ne 0) { throw "Graph $Method $Url failed." }
    }
    finally {
        if ($tempFile -and (Test-Path $tempFile)) { Remove-Item $tempFile -Force }
    }

    if ([string]::IsNullOrWhiteSpace($result)) { return $null }

    return $result | ConvertFrom-Json
}

# --------------------------------------------------------------------------- preflight
Write-Step 'Checking the Azure CLI session'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'The Azure CLI is not installed. See https://aka.ms/azure-cli'
}

$account = & az account show -o json 2>$null | ConvertFrom-Json
if (-not $account) { throw 'Not signed in. Run: az login' }

$tenantId = $account.tenantId
$signedInUser = Invoke-Graph -Method GET -Url 'https://graph.microsoft.com/v1.0/me?$select=id,displayName,userPrincipalName'

Write-Ok "Directory : $tenantId"
Write-Ok "Signed in : $($signedInUser.displayName) <$($signedInUser.userPrincipalName)>"

$apiName = "$NamePrefix API"
$spaName = "$NamePrefix SPA"

if (-not $Force) {
    $encoded = [uri]::EscapeDataString($apiName)
    $existing = Invoke-Graph -Method GET -Url "https://graph.microsoft.com/v1.0/applications?`$filter=displayName eq '$encoded'"

    if ($existing.value.Count -gt 0) {
        Write-Host ''
        Write-Warning "An app registration named '$apiName' already exists (appId $($existing.value[0].appId))."
        Write-Warning 'Re-run with -Force to create another, or -NamePrefix to use a different name.'
        return
    }
}

# --------------------------------------------------------------------------- API registration
Write-Step "Creating the API registration: $apiName"

$scopeId = [guid]::NewGuid().ToString()
$memberRoleId = [guid]::NewGuid().ToString()
$adminRoleId = [guid]::NewGuid().ToString()

$apiBody = @{
    displayName    = $apiName
    signInAudience = 'AzureADMyOrg'
    api            = @{
        # v2 tokens carry the client id as 'aud', which is what Microsoft.Identity.Web validates
        # by default. v1 would use the App ID URI instead and need extra configuration.
        requestedAccessTokenVersion = 2
        oauth2PermissionScopes      = @(
            @{
                id                      = $scopeId
                value                   = 'access_as_user'
                type                    = 'User'
                isEnabled               = $true
                adminConsentDisplayName = 'Access the API as the signed-in user'
                adminConsentDescription = 'Allows the SPA to call the API on behalf of the signed-in user.'
                userConsentDisplayName  = 'Access the API on your behalf'
                userConsentDescription  = 'Allows the app to call the API as you.'
            }
        )
    }
    # These 'value' strings are a contract with SharedKernel/RoleNames.cs. Renaming one here
    # without renaming it there leaves the person with a valid sign-in and no permissions.
    appRoles       = @(
        @{
            id                 = $memberRoleId
            value              = 'Member'
            displayName        = 'Member'
            description        = 'Can use the application.'
            allowedMemberTypes = @('User')
            isEnabled          = $true
        },
        @{
            id                 = $adminRoleId
            value              = 'Admin'
            displayName        = 'Administrator'
            description        = 'Can use the application and see every user.'
            allowedMemberTypes = @('User')
            isEnabled          = $true
        }
    )
}

$api = Invoke-Graph -Method POST -Url 'https://graph.microsoft.com/v1.0/applications' -Body $apiBody
Write-Ok "appId: $($api.appId)"

Invoke-Graph -Method PATCH -Url "https://graph.microsoft.com/v1.0/applications/$($api.id)" `
    -Body @{ identifierUris = @("api://$($api.appId)") } | Out-Null
Write-Ok "identifierUri: api://$($api.appId)"

$apiSp = Invoke-Graph -Method POST -Url 'https://graph.microsoft.com/v1.0/servicePrincipals' `
    -Body @{ appId = $api.appId; appRoleAssignmentRequired = $true }
Write-Ok 'Enterprise application created, assignment required = true'

# --------------------------------------------------------------------------- SPA registration
Write-Step "Creating the SPA registration: $spaName"

$spaBody = @{
    displayName            = $spaName
    signInAudience         = 'AzureADMyOrg'
    spa                    = @{ redirectUris = @($RedirectUri) }
    requiredResourceAccess = @(
        @{
            resourceAppId  = $api.appId
            resourceAccess = @(@{ id = $scopeId; type = 'Scope' })
        }
    )
}

$spa = Invoke-Graph -Method POST -Url 'https://graph.microsoft.com/v1.0/applications' -Body $spaBody
Write-Ok "appId: $($spa.appId)"
Write-Ok "redirectUri: $RedirectUri"

$spaSp = Invoke-Graph -Method POST -Url 'https://graph.microsoft.com/v1.0/servicePrincipals' `
    -Body @{ appId = $spa.appId }

# --------------------------------------------------------------------------- consent + roles
Write-Step 'Granting admin consent and assigning your roles'

Invoke-Graph -Method POST -Url 'https://graph.microsoft.com/v1.0/oauth2PermissionGrants' -Body @{
    clientId    = $spaSp.id
    consentType = 'AllPrincipals'
    resourceId  = $apiSp.id
    scope       = 'access_as_user'
} | Out-Null
Write-Ok 'Consent granted for access_as_user'

foreach ($role in @(@{ Name = 'Member'; Id = $memberRoleId }, @{ Name = 'Admin'; Id = $adminRoleId })) {
    Invoke-Graph -Method POST -Url "https://graph.microsoft.com/v1.0/servicePrincipals/$($apiSp.id)/appRoleAssignedTo" -Body @{
        principalId = $signedInUser.id
        resourceId  = $apiSp.id
        appRoleId   = $role.Id
    } | Out-Null
    Write-Ok "Assigned '$($role.Name)' to $($signedInUser.displayName)"
}

# --------------------------------------------------------------------------- local configuration
Write-Step 'Writing .NET user secrets'

Push-Location $webApiProject
try {
    & dotnet user-secrets set 'AzureAd:TenantId' $tenantId | Out-Null
    & dotnet user-secrets set 'AzureAd:ClientId' $api.appId | Out-Null
    & dotnet user-secrets set 'AzureAd:SpaClientId' $spa.appId | Out-Null
}
finally {
    Pop-Location
}

Write-Ok 'AzureAd:TenantId, AzureAd:ClientId and AzureAd:SpaClientId stored'
Write-Note 'User secrets live in your Windows profile, never in the repository.'
Write-Note 'The Angular app needs no configuration: it reads GET /auth-config from the API.'

# --------------------------------------------------------------------------- summary
Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host ''
Write-Host "  Tenant       $tenantId"
Write-Host "  API   appId  $($api.appId)   ($apiName)"
Write-Host "  SPA   appId  $($spa.appId)   ($spaName)"
Write-Host ''
Write-Host 'Next:'
Write-Host '  dotnet run --project src/backend/Aspire.AppHost'
Write-Host '  then open the Angular app - it will redirect you to Microsoft to sign in.'
Write-Host ''
Write-Host 'To give somebody else access:'
Write-Host "  Entra ID > Enterprise applications > '$apiName' > Users and groups > Add user/group"
Write-Host ''
Write-Host 'To undo:'
Write-Host "  az ad app delete --id $($api.appId)"
Write-Host "  az ad app delete --id $($spa.appId)"
