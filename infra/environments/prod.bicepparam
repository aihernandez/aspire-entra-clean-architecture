using '../main.bicep'

param workload = readEnvironmentVariable('AZURE_WORKLOAD', 'caeiat')
param environment = 'prod'
param addressSpace = '10.42.0.0/16'
param containerSubnetPrefix = '10.42.0.0/23'
param privateEndpointSubnetPrefix = '10.42.2.0/24'
param apiImage = readEnvironmentVariable('API_IMAGE')
param webImage = readEnvironmentVariable('WEB_IMAGE')
param tenantId = readEnvironmentVariable('ENTRA_TENANT_ID')
param apiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID')
param spaClientId = readEnvironmentVariable('ENTRA_SPA_CLIENT_ID')
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param alertEmail = readEnvironmentVariable('ALERT_EMAIL')
param enableStorage = false
param scaleMode = 'auto'
param minReplicas = 2
param maxReplicas = 10
param apiHttpConcurrency = 10
param webHttpConcurrency = 10
param tags = { application: 'clean-architecture-template', costCenter: 'template' }
