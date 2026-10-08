param name string
param location string
param tags object
param addressSpace string
param containerSubnetPrefix string
param privateEndpointSubnetPrefix string

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    addressSpace: { addressPrefixes: [addressSpace] }
    subnets: [
      {
        name: 'container-apps'
        properties: {
          addressPrefix: containerSubnetPrefix
          delegations: [{ name: 'container-apps', properties: { serviceName: 'Microsoft.App/environments' } }]
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

output vnetId string = vnet.id
output containerSubnetId string = '${vnet.id}/subnets/container-apps'
output privateEndpointSubnetId string = '${vnet.id}/subnets/private-endpoints'
