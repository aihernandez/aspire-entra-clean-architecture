param name string
param databaseName string
param location string
param tags object
param entraAdminObjectId string
param entraAdminName string
param tenantId string
param runtimeClientId string
param skuName string
param skuTier string
param maxSizeBytes int
param zoneRedundant bool
param backupRetentionDays int
param longTermRetention bool
param privateEndpointSubnetId string
param vnetId string
param actionGroupId string
param workspaceId string

resource server 'Microsoft.Sql/servers@2023-08-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      login: entraAdminName
      sid: entraAdminObjectId
      tenantId: tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: { name: skuName, tier: skuTier }
  properties: {
    maxSizeBytes: maxSizeBytes
    zoneRedundant: zoneRedundant
    requestedBackupStorageRedundancy: zoneRedundant ? 'Geo' : 'Local'
  }
}

resource shortRetention 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01' = {
  parent: database
  name: 'default'
  properties: { retentionDays: backupRetentionDays }
}

resource longRetention 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2023-08-01' = if (longTermRetention) {
  parent: database
  name: 'default'
  properties: {
    weeklyRetention: 'P4W'
    monthlyRetention: 'P12M'
    yearlyRetention: 'P0Y'
    weekOfYear: 1
  }
}

resource audit 'Microsoft.Sql/servers/auditingSettings@2023-08-01' = {
  parent: server
  name: 'default'
  properties: { state: 'Enabled', isAzureMonitorTargetEnabled: true }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-sql'
  scope: database
  properties: {
    workspaceId: workspaceId
    logs: [{ category: 'SQLSecurityAuditEvents', enabled: true }]
    metrics: [{ category: 'Basic', enabled: true }]
  }
}

resource dns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.${environment().suffixes.sqlServerHostname}'
  location: 'global'
  tags: tags
}
resource link 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: dns
  name: 'link-${name}'
  location: 'global'
  properties: { registrationEnabled: false, virtualNetwork: { id: vnetId } }
}
resource endpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-${name}'
  location: location
  tags: tags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [{
      name: 'sql'
      properties: { privateLinkServiceId: server.id, groupIds: ['sqlServer'] }
    }]
  }
}
resource zoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: endpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'sql', properties: { privateDnsZoneId: dns.id } }] }
}

resource cpuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(actionGroupId)) {
  name: 'alert-cpu-${name}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Azure SQL CPU average above 80 percent for 15 minutes.'
    severity: 2
    enabled: true
    scopes: [database.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [{
        name: 'HighCpu'
        criterionType: 'StaticThresholdCriterion'
        metricName: 'cpu_percent'
        metricNamespace: 'Microsoft.Sql/servers/databases'
        operator: 'GreaterThan'
        threshold: 80
        timeAggregation: 'Average'
      }]
    }
    actions: [{ actionGroupId: actionGroupId }]
  }
}

output serverName string = server.name
output databaseName string = database.name
// SqlClient 6.x accepts the client id of a user-assigned managed identity in User ID.
// This string contains no password or token and does not require a Key Vault secret.
output runtimeConnectionString string = 'Server=tcp:${server.name}.${environment().suffixes.sqlServerHostname},1433;Initial Catalog=${databaseName};Authentication=Active Directory Managed Identity;User ID=${runtimeClientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
