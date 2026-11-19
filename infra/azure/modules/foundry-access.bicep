// Lets the API's identity call the agents of an existing Foundry account, which this project does not
// create. Deployed into that account's own resource group.
param accountName string
param principalId string

// Foundry User (formerly Azure AI User): read the project and call its agents and models.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: accountName
}

resource user 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(account.id, principalId, foundryUserRoleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryUserRoleId)
  }
}
