// Container registry. No admin user: images are pushed with the deployer's Entra ID sign-in and
// pulled by the managed identities.
param location string
param tags object
param name string
param pullPrincipalIds array
param publicAccess bool
param privateEndpointSubnetId string
param privateDnsZoneId string

var usePrivateEndpoint = !empty(privateEndpointSubnetId)
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  tags: tags
  // Private endpoints need the Premium tier.
  sku: { name: usePrivateEndpoint ? 'Premium' : 'Basic' }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: publicAccess ? 'Enabled' : 'Disabled'
  }
}

resource pull 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in pullPrincipalIds: {
    scope: registry
    name: guid(registry.id, principalId, acrPullRoleId)
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    }
  }
]

module privateEndpoint 'private-endpoint.bicep' = if (usePrivateEndpoint) {
  name: 'registry-private-endpoint'
  params: {
    location: location
    tags: tags
    name: 'pe-${name}'
    subnetId: privateEndpointSubnetId
    targetResourceId: registry.id
    groupId: 'registry'
    privateDnsZoneIds: [privateDnsZoneId]
  }
}

output name string = registry.name
output loginServer string = registry.properties.loginServer
