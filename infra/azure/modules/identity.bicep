// Two user-assigned identities, so the AI workflow service holds no database or messaging access:
//   apps  the API, web and simulators: pulls images and reads Key Vault secrets
//   ai    the AI workflow service: pulls images and calls the model, nothing else
param location string
param tags object
param appsIdentityName string
param aiIdentityName string

resource appsIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: appsIdentityName
  location: location
  tags: tags
}

resource aiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: aiIdentityName
  location: location
  tags: tags
}

output appsId string = appsIdentity.id
output appsPrincipalId string = appsIdentity.properties.principalId
output appsClientId string = appsIdentity.properties.clientId
output aiId string = aiIdentity.id
output aiPrincipalId string = aiIdentity.properties.principalId
output aiClientId string = aiIdentity.properties.clientId
