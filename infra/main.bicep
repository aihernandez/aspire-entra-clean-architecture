targetScope = 'resourceGroup'

@description('Short globally reusable workload code, lowercase letters and digits only.')
@minLength(3)
@maxLength(12)
param workload string

@allowed(['dev', 'staging', 'prod'])
param environment string

param location string = resourceGroup().location
param tags object = {}
param addressSpace string
param containerSubnetPrefix string
param privateEndpointSubnetPrefix string
param apiImage string
param webImage string
param tenantId string
param apiClientId string
param spaClientId string
@description('Object id of the Entra group responsible for SQL bootstrap and migrations, never the API identity.')
param sqlEntraAdminObjectId string
param sqlEntraAdminName string
param smtpHost string = ''
param smtpFromEmail string = ''
param alertEmail string = ''
param enableStorage bool = false

@description('fixed keeps the configured replica count; auto uses explicit HTTP scaling rules.')
@allowed(['fixed', 'auto'])
param scaleMode string

@minValue(1)
param minReplicas int

@minValue(1)
param maxReplicas int

@minValue(1)
param apiHttpConcurrency int = 10

@minValue(1)
param webHttpConcurrency int = 10

var baseName = '${workload}-${environment}'
var commonTags = union(tags, { workload: workload, environment: environment, managedBy: 'bicep' })
var isProd = environment == 'prod'
var isDev = environment == 'dev'
var suffix = uniqueString(subscription().id, resourceGroup().id)

module network './modules/network.bicep' = {
  name: 'network-${baseName}'
  params: {
    name: 'vnet-${baseName}'
    location: location
    tags: commonTags
    addressSpace: addressSpace
    containerSubnetPrefix: containerSubnetPrefix
    privateEndpointSubnetPrefix: privateEndpointSubnetPrefix
  }
}

module monitor './modules/monitor.bicep' = {
  name: 'monitor-${baseName}'
  params: {
    name: baseName
    location: location
    tags: commonTags
    retentionDays: isProd ? 90 : 30
    alertEmail: alertEmail
  }
}

module apiIdentity './modules/identity.bicep' = {
  name: 'identity-api-${baseName}'
  params: { name: 'id-api-${baseName}', location: location, tags: commonTags }
}

module webIdentity './modules/identity.bicep' = {
  name: 'identity-web-${baseName}'
  params: { name: 'id-web-${baseName}', location: location, tags: commonTags }
}

module registry './modules/registry.bicep' = {
  name: 'registry-${baseName}'
  params: {
    name: 'acr${workload}${environment}${suffix}'
    location: location
    tags: commonTags
    principalIds: [apiIdentity.outputs.principalId, webIdentity.outputs.principalId]
  }
}

module vault './modules/key-vault.bicep' = {
  name: 'vault-${baseName}'
  params: {
    name: 'kv${take(workload, 6)}${environment}${take(suffix, 5)}'
    location: location
    tags: commonTags
    tenantId: tenantId
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    vnetId: network.outputs.vnetId
    purgeProtection: isProd
  }
}

module sql './modules/sql.bicep' = {
  name: 'sql-${baseName}'
  params: {
    name: 'sql-${baseName}-${suffix}'
    databaseName: 'app'
    location: location
    tags: commonTags
    entraAdminObjectId: sqlEntraAdminObjectId
    entraAdminName: sqlEntraAdminName
    tenantId: tenantId
    runtimeClientId: apiIdentity.outputs.clientId
    skuName: isProd ? 'GP_Gen5_2' : (isDev ? 'Basic' : 'S1')
    skuTier: isProd ? 'GeneralPurpose' : (isDev ? 'Basic' : 'Standard')
    maxSizeBytes: isProd ? 34359738368 : (isDev ? 2147483648 : 268435456000)
    zoneRedundant: isProd
    backupRetentionDays: isProd ? 35 : 7
    longTermRetention: isProd
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    vnetId: network.outputs.vnetId
    actionGroupId: monitor.outputs.actionGroupId
    workspaceId: monitor.outputs.workspaceId
  }
}

module storage './modules/storage.bicep' = if (enableStorage) {
  name: 'storage-${baseName}'
  params: {
    name: 'st${take(workload, 6)}${environment}${take(suffix, 5)}'
    location: location
    tags: commonTags
    skuName: isProd ? 'Standard_GZRS' : 'Standard_LRS'
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    vnetId: network.outputs.vnetId
    principalId: apiIdentity.outputs.principalId
  }
}

module apps './modules/container-apps.bicep' = {
  name: 'apps-${baseName}'
  params: {
    name: baseName
    location: location
    tags: commonTags
    containerSubnetId: network.outputs.containerSubnetId
    logAnalyticsCustomerId: monitor.outputs.workspaceCustomerId
    logAnalyticsSharedKey: monitor.outputs.workspaceSharedKey
    appInsightsConnectionString: monitor.outputs.appInsightsConnectionString
    registryServer: registry.outputs.loginServer
    apiIdentityId: apiIdentity.outputs.id
    webIdentityId: webIdentity.outputs.id
    apiImage: apiImage
    webImage: webImage
    scaleMode: scaleMode
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    apiHttpConcurrency: apiHttpConcurrency
    webHttpConcurrency: webHttpConcurrency
    zoneRedundant: isProd
    databaseConnectionString: sql.outputs.runtimeConnectionString
    tenantId: tenantId
    apiClientId: apiClientId
    spaClientId: spaClientId
    smtpHost: smtpHost
    smtpFromEmail: smtpFromEmail
  }
}

module edge './modules/front-door.bicep' = {
  name: 'edge-${baseName}'
  params: {
    name: '${baseName}-${take(suffix, 5)}'
    tags: commonTags
    privateLinkLocation: location
    environmentId: apps.outputs.environmentId
    apiHost: apps.outputs.apiHost
    webHost: apps.outputs.webHost
    wafMode: isProd ? 'Prevention' : 'Detection'
    workspaceId: monitor.outputs.workspaceId
  }
}

output apiUrl string = edge.outputs.apiUrl
output webUrl string = edge.outputs.webUrl
output registryServer string = registry.outputs.loginServer
output keyVaultName string = vault.outputs.name
output sqlServerName string = sql.outputs.serverName
output sqlDatabaseName string = sql.outputs.databaseName
output apiIdentityPrincipalId string = apiIdentity.outputs.principalId
output apiIdentityClientId string = apiIdentity.outputs.clientId
output webIdentityPrincipalId string = webIdentity.outputs.principalId
output applicationInsightsConnectionString string = monitor.outputs.appInsightsConnectionString
output frontDoorPrivateLinkApprovalTarget string = apps.outputs.environmentId
