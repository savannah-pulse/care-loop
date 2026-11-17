// Virtual network for private networking: one subnet for the Container Apps environment, one for
// private endpoints, and the private DNS zones those endpoints register in.
param location string
param tags object
param name string
param addressPrefix string = '10.40.0.0/16'
param appsSubnetPrefix string = '10.40.0.0/23'
param privateEndpointSubnetPrefix string = '10.40.2.0/24'

var zoneNames = {
  postgres: 'privatelink.postgres.database.azure.com'
  keyVault: 'privatelink.vaultcore.azure.net'
  registry: 'privatelink${environment().suffixes.acrLoginServer}'
  cognitiveServices: 'privatelink.cognitiveservices.azure.com'
  openAi: 'privatelink.openai.azure.com'
  aiServices: 'privatelink.services.ai.azure.com'
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    addressSpace: { addressPrefixes: [addressPrefix] }
    subnets: [
      {
        name: 'snet-apps'
        properties: {
          addressPrefix: appsSubnetPrefix
          delegations: [
            { name: 'container-apps', properties: { serviceName: 'Microsoft.App/environments' } }
          ]
        }
      }
      {
        name: 'snet-private-endpoints'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
        }
      }
    ]
  }
}

resource zones 'Microsoft.Network/privateDnsZones@2024-06-01' = [
  for zone in items(zoneNames): {
    name: zone.value
    location: 'global'
    tags: tags
  }
]

resource links 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = [
  for (zone, i) in items(zoneNames): {
    parent: zones[i]
    name: '${name}-link'
    location: 'global'
    tags: tags
    properties: {
      registrationEnabled: false
      virtualNetwork: { id: vnet.id }
    }
  }
]

output appsSubnetId string = vnet.properties.subnets[0].id
output privateEndpointSubnetId string = vnet.properties.subnets[1].id
output postgresDnsZoneId string = resourceId('Microsoft.Network/privateDnsZones', zoneNames.postgres)
output keyVaultDnsZoneId string = resourceId('Microsoft.Network/privateDnsZones', zoneNames.keyVault)
output registryDnsZoneId string = resourceId('Microsoft.Network/privateDnsZones', zoneNames.registry)
output aiDnsZoneIds array = [
  resourceId('Microsoft.Network/privateDnsZones', zoneNames.cognitiveServices)
  resourceId('Microsoft.Network/privateDnsZones', zoneNames.openAi)
  resourceId('Microsoft.Network/privateDnsZones', zoneNames.aiServices)
]
