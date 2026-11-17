// Key Vault holding the three secrets of a deployment. The container apps read them through
// Key Vault references with the apps identity; nothing secret is stored on the apps themselves.
param location string
param tags object
param name string
param purgeProtection bool
param readerPrincipalId string
param deployerPrincipalId string
@secure()
param postgresAdminPassword string
@secure()
param connectionString string
@secure()
param simulatorInboundSecret string
param privateEndpointSubnetId string
param privateDnsZoneId string

var usePrivateEndpoint = !empty(privateEndpointSubnetId)
var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    // Purge protection can be turned on but never off, so it is left unset rather than set to false.
    enablePurgeProtection: purgeProtection ? true : null
    publicNetworkAccess: usePrivateEndpoint ? 'Disabled' : 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: usePrivateEndpoint ? 'Deny' : 'Allow'
    }
  }
}

// Kept so scripts/deploy.sh can reuse the password on the next run instead of rotating it.
resource adminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-admin-password'
  properties: { value: postgresAdminPassword }
}

resource connectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'careloop-connection-string'
  properties: { value: connectionString }
}

resource inboundSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'simulator-inbound-secret'
  properties: { value: simulatorInboundSecret }
}

resource reader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, readerPrincipalId, secretsUserRoleId)
  properties: {
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
  }
}

resource deployer 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  scope: vault
  name: guid(vault.id, deployerPrincipalId, secretsOfficerRoleId)
  properties: {
    principalId: deployerPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
  }
}

module privateEndpoint 'private-endpoint.bicep' = if (usePrivateEndpoint) {
  name: 'keyvault-private-endpoint'
  params: {
    location: location
    tags: tags
    name: 'pe-${name}'
    subnetId: privateEndpointSubnetId
    targetResourceId: vault.id
    groupId: 'vault'
    privateDnsZoneIds: [privateDnsZoneId]
  }
}

output name string = vault.name
// Unversioned, so the apps pick up a rotated secret without a template change.
output connectionStringSecretUri string = connectionStringSecret.properties.secretUri
output inboundSecretUri string = inboundSecret.properties.secretUri
