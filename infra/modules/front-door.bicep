param name string
param tags object
param privateLinkLocation string
param environmentId string
param apiHost string
param webHost string
@allowed(['Detection', 'Prevention'])
param wafMode string
param workspaceId string

resource profile 'Microsoft.Cdn/profiles@2024-02-01' = {
  name: 'afd-${name}'
  location: 'global'
  tags: tags
  sku: { name: 'Premium_AzureFrontDoor' }
}

resource waf 'Microsoft.Network/FrontDoorWebApplicationFirewallPolicies@2024-02-01' = {
  name: 'waf${replace(name, '-', '')}'
  location: 'global'
  tags: tags
  sku: { name: 'Premium_AzureFrontDoor' }
  properties: {
    policySettings: {
      enabledState: 'Enabled'
      mode: wafMode
      requestBodyCheck: 'Enabled'
    }
    managedRules: {
      managedRuleSets: [
        { ruleSetType: 'Microsoft_DefaultRuleSet', ruleSetVersion: '2.1', ruleSetAction: 'Block' }
        { ruleSetType: 'Microsoft_BotManagerRuleSet', ruleSetVersion: '1.0', ruleSetAction: 'Block' }
      ]
    }
  }
}

resource webEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-02-01' = {
  parent: profile
  name: 'web-${name}'
  location: 'global'
  properties: { enabledState: 'Enabled' }
}

resource apiRules 'Microsoft.Cdn/profiles/ruleSets@2024-02-01' = {
  parent: profile
  name: 'stripApiPrefix'
}
resource rewriteApi 'Microsoft.Cdn/profiles/ruleSets/rules@2024-02-01' = {
  parent: apiRules
  name: 'rewrite'
  properties: {
    order: 1
    conditions: []
    actions: [{
      name: 'UrlRewrite'
      parameters: {
        typeName: 'DeliveryRuleUrlRewriteActionParameters'
        sourcePattern: '/api/'
        destination: '/'
        preserveUnmatchedPath: true
      }
    }]
  }
}

resource apiGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: profile
  name: 'api'
  properties: {
    healthProbeSettings: { probePath: '/health', probeRequestType: 'GET', probeProtocol: 'Https', probeIntervalInSeconds: 60 }
    loadBalancingSettings: { sampleSize: 4, successfulSamplesRequired: 3, additionalLatencyInMilliseconds: 50 }
  }
}
resource webGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: profile
  name: 'web'
  properties: {
    healthProbeSettings: { probePath: '/', probeRequestType: 'GET', probeProtocol: 'Https', probeIntervalInSeconds: 60 }
    loadBalancingSettings: { sampleSize: 4, successfulSamplesRequired: 3, additionalLatencyInMilliseconds: 50 }
  }
}

resource apiOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: apiGroup
  name: 'api'
  properties: {
    hostName: apiHost
    originHostHeader: apiHost
    httpPort: 80
    httpsPort: 443
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
    sharedPrivateLinkResource: {
      privateLink: { id: environmentId }
      privateLinkLocation: privateLinkLocation
      groupId: 'managedEnvironments'
      requestMessage: 'Approve Azure Front Door origin for ${name}'
    }
  }
}
resource webOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: webGroup
  name: 'web'
  properties: {
    hostName: webHost
    originHostHeader: webHost
    httpPort: 80
    httpsPort: 443
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
    sharedPrivateLinkResource: {
      privateLink: { id: environmentId }
      privateLinkLocation: privateLinkLocation
      groupId: 'managedEnvironments'
      requestMessage: 'Approve Azure Front Door origin for ${name}'
    }
  }
}

resource apiRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: webEndpoint
  name: 'api'
  dependsOn: [apiOrigin, rewriteApi]
  properties: {
    originGroup: { id: apiGroup.id }
    supportedProtocols: ['Http', 'Https']
    patternsToMatch: ['/api/*']
    ruleSets: [{ id: apiRules.id }]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    enabledState: 'Enabled'
  }
}
resource webRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: webEndpoint
  name: 'all'
  dependsOn: [webOrigin, apiRoute]
  properties: {
    originGroup: { id: webGroup.id }
    supportedProtocols: ['Http', 'Https']
    patternsToMatch: ['/*']
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    enabledState: 'Enabled'
  }
}

resource policy 'Microsoft.Cdn/profiles/securityPolicies@2024-02-01' = {
  parent: profile
  name: 'default'
  properties: {
    parameters: {
      type: 'WebApplicationFirewall'
      wafPolicy: { id: waf.id }
      associations: [
        { domains: [{ id: webEndpoint.id }], patternsToMatch: ['/*'] }
      ]
    }
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-front-door'
  scope: profile
  properties: {
    workspaceId: workspaceId
    logs: [
      { category: 'FrontDoorAccessLog', enabled: true }
      { category: 'FrontDoorHealthProbeLog', enabled: true }
      { category: 'FrontDoorWebApplicationFirewallLog', enabled: true }
    ]
  }
}

output apiUrl string = 'https://${webEndpoint.properties.hostName}/api'
output webUrl string = 'https://${webEndpoint.properties.hostName}'
