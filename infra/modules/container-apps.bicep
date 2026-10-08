param name string
param location string
param tags object
param containerSubnetId string
param logAnalyticsCustomerId string
@secure()
param logAnalyticsSharedKey string
param appInsightsConnectionString string
param registryServer string
param identityId string
param apiImage string
param webImage string
@allowed(['fixed', 'auto'])
param scaleMode string
@minValue(1)
param minReplicas int
@minValue(1)
param maxReplicas int
@minValue(1)
param apiHttpConcurrency int
@minValue(1)
param webHttpConcurrency int
param zoneRedundant bool
param connectionSecretUri string
param tenantId string
param apiClientId string
param spaClientId string
param smtpHost string
param smtpFromEmail string

resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: 'cae-${name}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsSharedKey
      }
    }
    vnetConfiguration: { infrastructureSubnetId: containerSubnetId, internal: true }
    publicNetworkAccess: 'Disabled'
    zoneRedundant: zoneRedundant
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-api-${name}'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identityId}': {} } }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [{ server: registryServer, identity: identityId }]
      secrets: [{ name: 'database', keyVaultUrl: connectionSecretUri, identity: identityId }]
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
    }
    template: {
      containers: [{
        name: 'api'
        image: apiImage
        env: [
          { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
          { name: 'ConnectionStrings__Database', secretRef: 'database' }
          { name: 'AzureAd__Instance', value: 'https://login.microsoftonline.com/' }
          { name: 'AzureAd__TenantId', value: tenantId }
          { name: 'AzureAd__ClientId', value: apiClientId }
          { name: 'AzureAd__SpaClientId', value: spaClientId }
          { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
          { name: 'Smtp__Host', value: smtpHost }
          { name: 'Smtp__FromEmail', value: smtpFromEmail }
        ]
        resources: { cpu: json('0.5'), memory: '1Gi' }
        probes: [
          { type: 'Liveness', httpGet: { path: '/health', port: 8080 }, initialDelaySeconds: 30, periodSeconds: 30 }
          { type: 'Readiness', httpGet: { path: '/health', port: 8080 }, initialDelaySeconds: 10, periodSeconds: 15 }
        ]
      }]
      scale: {
        minReplicas: minReplicas
        maxReplicas: scaleMode == 'auto' ? maxReplicas : minReplicas
        rules: scaleMode == 'auto' ? [
          {
            name: 'http-api'
            http: { metadata: { concurrentRequests: string(apiHttpConcurrency) } }
          }
        ] : []
      }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-web-${name}'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identityId}': {} } }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [{ server: registryServer, identity: identityId }]
      ingress: { external: true, targetPort: 80, transport: 'http', allowInsecure: false }
    }
    template: {
      containers: [{ name: 'web', image: webImage, resources: { cpu: json('0.25'), memory: '0.5Gi' } }]
      scale: {
        minReplicas: minReplicas
        maxReplicas: scaleMode == 'auto' ? maxReplicas : minReplicas
        rules: scaleMode == 'auto' ? [
          {
            name: 'http-web'
            http: { metadata: { concurrentRequests: string(webHttpConcurrency) } }
          }
        ] : []
      }
    }
  }
}

output environmentId string = environment.id
output apiHost string = api.properties.configuration.ingress.fqdn
output webHost string = web.properties.configuration.ingress.fqdn
