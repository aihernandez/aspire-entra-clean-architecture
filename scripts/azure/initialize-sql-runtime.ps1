<#
.SYNOPSIS
    Creates an Azure SQL contained user for the API managed identity, after migrations.
.DESCRIPTION
    Run as a member of the configured SQL Entra administrator group, from a private-network runner.
    Requires Azure CLI and SqlServer PowerShell module 22+. Uses the current Azure CLI identity;
    never takes a SQL password. RenderOnly emits the SQL without contacting Azure or a database.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9.-]*$')]
    [string]$Server,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')]
    [string]$Database = 'app',
    [Parameter(Mandatory)]
    [guid]$ApiPrincipalId,
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_-]{0,127}$')]
    [string]$UserName = 'api-runtime',
    [switch]$RenderOnly
)
$ErrorActionPreference = 'Stop'
if ($ApiPrincipalId -eq [guid]::Empty) { throw 'ApiPrincipalId must be the managed identity object id.' }
$sid = '0x' + (($ApiPrincipalId.ToByteArray() | ForEach-Object { $_.ToString('X2') }) -join '')

# SID + TYPE avoid directory name lookup and do not require Directory Readers on the SQL server.
# Names are restricted above; the only interpolated SID is rendered from a Guid.
$query = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.TodoItems', N'U') IS NULL
    THROW 50001, 'Apply EF migrations with the migration identity before granting runtime access.', 1;
IF DATABASE_PRINCIPAL_ID(N'$UserName') IS NULL
    CREATE USER [$UserName] WITH SID = $sid, TYPE = E;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$UserName' AND sid = $sid AND type = 'E')
    THROW 50002, 'Existing runtime user has a different identity or principal type. Review manually.', 1;
IF EXISTS (SELECT 1 FROM sys.database_role_members WHERE member_principal_id = DATABASE_PRINCIPAL_ID(N'$UserName'))
    THROW 50003, 'Runtime user already has role memberships. Review privileges before continuing.', 1;
IF EXISTS (
    SELECT 1 FROM sys.database_permissions
    WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$UserName') AND state IN ('G', 'W')
      AND NOT (state = 'G' AND (
          (class = 0 AND permission_name = 'CONNECT') OR
          (class = 1 AND major_id IN (OBJECT_ID(N'dbo.Users'), OBJECT_ID(N'dbo.TodoItems'))
           AND minor_id = 0 AND permission_name IN ('SELECT', 'INSERT', 'UPDATE', 'DELETE')))))
    THROW 50004, 'Runtime user has unexpected explicit grants. Review privileges before continuing.', 1;
GRANT CONNECT TO [$UserName];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[Users] TO [$UserName];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[TodoItems] TO [$UserName];
COMMIT TRANSACTION;
SELECT name, type_desc FROM sys.database_principals WHERE name = N'$UserName';
"@

if ($RenderOnly) { Write-Output $query; return }

Import-Module SqlServer -MinimumVersion 22.0.0 -ErrorAction Stop
$sqlToken = az account get-access-token --resource https://database.windows.net/ --query accessToken --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sqlToken)) {
    throw 'Cannot acquire an Azure SQL token using the current Azure CLI identity.'
}
try {
    Invoke-Sqlcmd -ServerInstance "tcp:$Server,1433" -Database $Database -AccessToken $sqlToken `
        -Query $query -AbortOnError -ConnectionTimeout 30 -QueryTimeout 60 -Encrypt Mandatory `
        -TrustServerCertificate:$false
}
finally { $sqlToken = $null }
