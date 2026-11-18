// Microsoft Foundry account, project and one model deployment, with a guardrail that blocks prompt
// attacks. Keys are disabled: callers sign in with a managed identity holding the Foundry User role.
param location string
param privateEndpointLocation string
param tags object
param accountName string
param projectName string
param modelName string
param modelVersion string
param modelSku string
param modelCapacity int
param userPrincipalIds array
param privateEndpointSubnetId string
param privateDnsZoneIds array

var usePrivateEndpoint = !empty(privateEndpointSubnetId)
// Foundry User (formerly Azure AI User): read the project and call its models.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'
var guardrailName = 'careloop-prompt-shields'

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: accountName
    allowProjectManagement: true
    disableLocalAuth: true
    publicNetworkAccess: usePrivateEndpoint ? 'Disabled' : 'Enabled'
    networkAcls: {
      defaultAction: usePrivateEndpoint ? 'Deny' : 'Allow'
    }
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    displayName: 'CareLoop'
    description: 'CareLoop AI workflow service'
  }
}

// The AI workflow service requires Prompt Shields for user and document (indirect) attacks, both
// blocking, on the model deployment. This creates that guardrail; an operator still has to verify it
// in the Foundry guardrail playground before setting aiPromptShieldsAttested.
resource guardrail 'Microsoft.CognitiveServices/accounts/raiPolicies@2025-06-01' = {
  parent: account
  name: guardrailName
  properties: {
    basePolicyName: 'Microsoft.DefaultV2'
    mode: 'Default'
    contentFilters: [
      { name: 'Hate', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Hate', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Sexual', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Sexual', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Violence', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Violence', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Selfharm', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Selfharm', severityThreshold: 'Medium', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Jailbreak', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Indirect Attack', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Protected Material Text', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Protected Material Code', blocking: false, enabled: true, source: 'Completion' }
    ]
  }
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: modelName
  sku: {
    name: modelSku
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
    raiPolicyName: guardrail.name
  }
  dependsOn: [project]
}

resource users 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in userPrincipalIds: {
    scope: account
    name: guid(account.id, principalId, foundryUserRoleId)
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryUserRoleId)
    }
  }
]

module privateEndpoint 'private-endpoint.bicep' = if (usePrivateEndpoint) {
  name: 'ai-private-endpoint'
  params: {
    location: privateEndpointLocation
    tags: tags
    name: 'pe-${accountName}'
    subnetId: privateEndpointSubnetId
    targetResourceId: account.id
    groupId: 'account'
    privateDnsZoneIds: privateDnsZoneIds
  }
}

output projectEndpoint string = 'https://${account.properties.customSubDomainName}.services.ai.azure.com/api/projects/${project.name}'
output accountEndpoint string = account.properties.endpoint
output deploymentName string = deployment.name
