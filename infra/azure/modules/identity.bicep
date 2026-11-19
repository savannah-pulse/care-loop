// One user-assigned identity for the API, web app and simulators: it pulls images and reads Key Vault
// secrets. A future direct AI provider would use this identity to call its configured model.
param location string
param tags object
param appsIdentityName string

resource appsIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: appsIdentityName
  location: location
  tags: tags
}

output appsId string = appsIdentity.id
output appsPrincipalId string = appsIdentity.properties.principalId
output appsClientId string = appsIdentity.properties.clientId
