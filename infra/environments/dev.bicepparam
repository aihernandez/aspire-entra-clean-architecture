using '../main.bicep'

param workload = readEnvironmentVariable('AZURE_WORKLOAD', 'caeiat')
param environment = 'dev'
param addressSpace = '10.40.0.0/16'
param containerSubnetPrefix = '10.40.0.0/23'
param privateEndpointSubnetPrefix = '10.40.2.0/24'
param apiImage = readEnvironmentVariable('API_IMAGE')
param webImage = readEnvironmentVariable('WEB_IMAGE')
param tenantId = readEnvironmentVariable('ENTRA_TENANT_ID')
param apiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID')
param spaClientId = readEnvironmentVariable('ENTRA_SPA_CLIENT_ID')
param sqlEntraAdminObjectId = readEnvironmentVariable('SQL_ENTRA_ADMIN_OBJECT_ID')
param sqlEntraAdminName = readEnvironmentVariable('SQL_ENTRA_ADMIN_NAME')
param alertEmail = readEnvironmentVariable('ALERT_EMAIL', '')
param enableStorage = false
param scaleMode = 'fixed'
param minReplicas = 1
param maxReplicas = 3
param apiHttpConcurrency = 10
param webHttpConcurrency = 10
param tags = { application: 'clean-architecture-template', costCenter: 'template' }
