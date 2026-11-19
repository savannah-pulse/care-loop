// Optional: a Microsoft Foundry account, project and one model deployment, with a guardrail that
// blocks prompt attacks. Used only when createFoundry is true, for a subscription that has no
// Foundry to use. Keys are disabled. The deployment does not grant model access to a runtime
// identity; the account owner must authorize the intended caller when an AI provider is enabled.
param location string
param tags object
param accountName string
param projectName string
param modelName string
param modelVersion string
param modelSku string
param modelCapacity int
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
    publicNetworkAccess: 'Enabled'
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
    description: 'CareLoop'
  }
}

// Prompt Shields block user and document (indirect) attacks on the model deployment.
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
  // The account accepts one change at a time: created beside the project, one of the two fails
  // with "Another operation is in progress". So project, then guardrail, then model deployment.
  dependsOn: [project]
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
}

output projectEndpoint string = 'https://${account.properties.customSubDomainName}.services.ai.azure.com/api/projects/${project.name}'
output deploymentName string = deployment.name
