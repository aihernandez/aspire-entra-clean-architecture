param name string
param location string
param tags object
param retentionDays int
param alertEmail string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-${name}'
  location: location
  tags: tags
  properties: { retentionInDays: retentionDays, sku: { name: 'PerGB2018' } }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    DisableLocalAuth: true
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmail)) {
  name: 'ag-${name}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'appalert'
    enabled: true
    emailReceivers: [{ name: 'operations', emailAddress: alertEmail, useCommonAlertSchema: true }]
  }
}

output workspaceCustomerId string = workspace.properties.customerId
output workspaceId string = workspace.id
@secure()
output workspaceSharedKey string = listKeys(workspace.id, workspace.apiVersion).primarySharedKey
output appInsightsConnectionString string = insights.properties.ConnectionString
output actionGroupId string = empty(alertEmail) ? '' : actionGroup!.id
