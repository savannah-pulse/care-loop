// CareLoop on Azure: one subscription-scope deployment that creates a resource group and everything in it.
//
//   az deployment sub create --location <region> --parameters environments/<name>.bicepparam
//
// Run it through scripts/deploy.sh, which also builds and pushes the images. The deployment is in two
// passes because the container apps need images that only exist once the registry does:
//   pass 1  imageTag = ''     platform only (registry, database, Key Vault, Container Apps environment, ...)
//   pass 2  imageTag = <tag>  the same platform plus the container apps running that tag
targetScope = 'subscription'

// ---------------------------------------------------------------------------------------------------
// What and where
// ---------------------------------------------------------------------------------------------------

@description('Short environment name, for example dev, test or prod. Part of every resource name.')
@minLength(2)
@maxLength(8)
param environmentName string

@description('Azure region for every resource except, optionally, the AI resources (see aiLocation).')
param location string = deployment().location

@description('Lower-case letters and digits only. Prefix of every resource name.')
@minLength(2)
@maxLength(8)
param namePrefix string = 'careloop'

@description('Extra tags for every resource.')
param tags object = {}

// ---------------------------------------------------------------------------------------------------
// Switches
// ---------------------------------------------------------------------------------------------------

@description('false: public endpoints protected by firewall rules and Entra ID. true: a virtual network, with private endpoints for PostgreSQL, Key Vault, the registry and the AI account. Use true before any real patient data.')
param privateNetworking bool = false

@description('Deploy the HIS and messaging simulators, so the environment can run the demo scenarios.')
param deploySimulators bool = true

@description('Deploy the Python AI workflow service and a Microsoft Foundry account, project and model deployment.')
param deployAi bool = true

@description('Deploy a Log Analytics workspace and Application Insights, and send container logs there.')
param deployMonitoring bool = true

// ---------------------------------------------------------------------------------------------------
// Application
// ---------------------------------------------------------------------------------------------------

@description('Image tag to run. Empty deploys the platform only (pass 1); scripts/deploy.sh sets it for pass 2.')
param imageTag string = ''

@description('Give the API its own public address. Off by default: the dashboard reaches it through the web app on /api/, and the API has demo-level authentication only. A real hospital system that posts to /fhir needs this on.')
param exposeApi bool = false

@description('Give the simulators public addresses so their screens can be opened in a browser. They hold synthetic data only.')
param exposeSimulators bool = true

@description('CIDR ranges allowed to reach the public addresses. Empty allows everyone.')
param allowedIpRanges array = []

@description('Use the demo pathway (deadlines divided by 1800) and a one-second deadline sweep. Must be true wherever the simulators run scenarios, false for real use.')
param demoTiming bool = deploySimulators

@description('Apply EF Core migrations when the API starts. Nothing else in this project applies them.')
param migrateOnStartup bool = true

@description('The API setting Ai:Provider. Leave NoOp until WS9 finishes the provider you choose.')
@allowed(['NoOp', 'AiWorkflow', 'FoundryDirect'])
param apiAiProvider string = 'NoOp'

@description('Time zone used in messages to patients and staff.')
param notificationsTimeZone string = 'Asia/Dubai'

@description('Extra API settings as { name, value } pairs, for example a facility name or a staff contact.')
param apiExtraEnv array = []

@description('Most web replicas under load.')
param webMaxReplicas int = 3

// ---------------------------------------------------------------------------------------------------
// Secrets (never put values in a parameter file; scripts/deploy.sh and the workflow pass them in)
// ---------------------------------------------------------------------------------------------------

@description('PostgreSQL administrator password. Letters and digits only, because it is placed in a connection string.')
@secure()
@minLength(16)
param postgresAdminPassword string

@description('Secret that signs the messaging callbacks to /notifications/inbound. The API refuses every inbound message without it.')
@secure()
@minLength(16)
param simulatorInboundSecret string

@description('Object ID of the person or service principal deploying. When set, it can read and write the Key Vault secrets, which lets scripts/deploy.sh reuse them on the next run.')
param deployerPrincipalId string = ''

// ---------------------------------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------------------------------

param postgresAdminLogin string = 'careloopadmin'
param postgresDatabaseName string = 'careloop'
param postgresVersion string = '17'

@description('Compute size, for example Standard_B1ms (Burstable) or Standard_D2ds_v5 (GeneralPurpose).')
param postgresSkuName string = 'Standard_B1ms'

@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param postgresSkuTier string = 'Burstable'

param postgresStorageGb int = 32

@minValue(7)
@maxValue(35)
param postgresBackupRetentionDays int = 7

param postgresGeoRedundantBackup bool = false

@description('Not available on the Burstable tier.')
@allowed(['Disabled', 'SameZone', 'ZoneRedundant'])
param postgresHighAvailability string = 'Disabled'

@description('Npgsql SSL Mode. VerifyFull checks the server certificate and name.')
@allowed(['Require', 'VerifyCA', 'VerifyFull'])
param postgresSslMode string = 'VerifyFull'

// ---------------------------------------------------------------------------------------------------
// Key Vault and registry
// ---------------------------------------------------------------------------------------------------

@description('Stop a deleted Key Vault from being purged for 90 days. Cannot be turned off again; leave false for throwaway environments.')
param keyVaultPurgeProtection bool = false

@description('With private networking, keep the registry reachable from the internet (Entra ID sign-in still required) so a laptop or a GitHub-hosted runner can push images. Pulls use the private endpoint either way.')
param registryPublicAccess bool = true

// ---------------------------------------------------------------------------------------------------
// AI (used only when deployAi is true)
// ---------------------------------------------------------------------------------------------------

@description('Region of the Foundry account. The model must be offered there; check data residency before choosing a region other than location.')
param aiLocation string = location

param aiModelName string = 'gpt-4.1-mini'
param aiModelVersion string = '2025-04-14'

@description('GlobalStandard may process prompts in any Azure region. Use DataZoneStandard or Standard where residency matters.')
param aiModelSku string = 'GlobalStandard'

@description('Thousands of tokens per minute.')
param aiModelCapacity int = 10

@description('Turn inference on in the AI workflow service (CARELOOP_AI_PROVIDER = foundry). Off, the service runs but drafts nothing.')
param aiInferenceEnabled bool = false

@description('Operator attestation that Prompt Shields block user and document attacks on the model deployment. Verify it in the Foundry guardrail playground first; the service refuses to call Foundry without it.')
param aiPromptShieldsAttested bool = false

param logRetentionDays int = 30

// ---------------------------------------------------------------------------------------------------
// Names
// ---------------------------------------------------------------------------------------------------

var token = toLower(uniqueString(subscription().subscriptionId, namePrefix, environmentName, location))
var baseName = '${namePrefix}-${environmentName}'
var allTags = union({ application: namePrefix, environment: environmentName, 'managed-by': 'bicep' }, tags)

var names = {
  resourceGroup: 'rg-${baseName}'
  logAnalytics: 'log-${baseName}'
  appInsights: 'appi-${baseName}'
  virtualNetwork: 'vnet-${baseName}'
  appsIdentity: 'id-${baseName}-apps'
  aiIdentity: 'id-${baseName}-ai'
  // Registry and Key Vault names are global and short, so they carry the token instead of the environment.
  registry: 'cr${namePrefix}${token}'
  keyVault: 'kv-${namePrefix}-${take(token, 10)}'
  postgres: 'psql-${baseName}-${token}'
  aiAccount: 'ai-${baseName}-${token}'
  aiProject: 'careloop'
  containerEnvironment: 'cae-${baseName}'
}

var deployApps = !empty(imageTag)

// ---------------------------------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------------------------------

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: names.resourceGroup
  location: location
  tags: allTags
}

module monitoring 'modules/monitoring.bicep' = if (deployMonitoring) {
  scope: rg
  name: 'monitoring'
  params: {
    location: location
    tags: allTags
    workspaceName: names.logAnalytics
    appInsightsName: names.appInsights
    retentionDays: logRetentionDays
  }
}

module network 'modules/network.bicep' = if (privateNetworking) {
  scope: rg
  name: 'network'
  params: {
    location: location
    tags: allTags
    name: names.virtualNetwork
  }
}

module identity 'modules/identity.bicep' = {
  scope: rg
  name: 'identity'
  params: {
    location: location
    tags: allTags
    appsIdentityName: names.appsIdentity
    aiIdentityName: names.aiIdentity
  }
}

module registry 'modules/registry.bicep' = {
  scope: rg
  name: 'registry'
  params: {
    location: location
    tags: allTags
    name: names.registry
    pullPrincipalIds: [identity.outputs.appsPrincipalId, identity.outputs.aiPrincipalId]
    publicAccess: !privateNetworking || registryPublicAccess
    privateEndpointSubnetId: privateNetworking ? network!.outputs.privateEndpointSubnetId : ''
    privateDnsZoneId: privateNetworking ? network!.outputs.registryDnsZoneId : ''
  }
}

module postgres 'modules/postgres.bicep' = {
  scope: rg
  name: 'postgres'
  params: {
    location: location
    tags: allTags
    name: names.postgres
    version: postgresVersion
    administratorLogin: postgresAdminLogin
    administratorPassword: postgresAdminPassword
    databaseName: postgresDatabaseName
    skuName: postgresSkuName
    skuTier: postgresSkuTier
    storageGb: postgresStorageGb
    backupRetentionDays: postgresBackupRetentionDays
    geoRedundantBackup: postgresGeoRedundantBackup
    highAvailability: postgresHighAvailability
    privateEndpointSubnetId: privateNetworking ? network!.outputs.privateEndpointSubnetId : ''
    privateDnsZoneId: privateNetworking ? network!.outputs.postgresDnsZoneId : ''
  }
}

module keyVault 'modules/keyvault.bicep' = {
  scope: rg
  name: 'keyvault'
  params: {
    location: location
    tags: allTags
    name: names.keyVault
    purgeProtection: keyVaultPurgeProtection
    readerPrincipalId: identity.outputs.appsPrincipalId
    deployerPrincipalId: deployerPrincipalId
    postgresAdminPassword: postgresAdminPassword
    connectionString: 'Host=${postgres.outputs.fqdn};Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=${postgresSslMode}'
    simulatorInboundSecret: simulatorInboundSecret
    privateEndpointSubnetId: privateNetworking ? network!.outputs.privateEndpointSubnetId : ''
    privateDnsZoneId: privateNetworking ? network!.outputs.keyVaultDnsZoneId : ''
  }
}

module ai 'modules/ai.bicep' = if (deployAi) {
  scope: rg
  name: 'ai'
  params: {
    location: aiLocation
    privateEndpointLocation: location
    tags: allTags
    accountName: names.aiAccount
    projectName: names.aiProject
    modelName: aiModelName
    modelVersion: aiModelVersion
    modelSku: aiModelSku
    modelCapacity: aiModelCapacity
    // The AI workflow service always may call the model; the API only on the FoundryDirect route.
    userPrincipalIds: apiAiProvider == 'FoundryDirect'
      ? [identity.outputs.aiPrincipalId, identity.outputs.appsPrincipalId]
      : [identity.outputs.aiPrincipalId]
    privateEndpointSubnetId: privateNetworking ? network!.outputs.privateEndpointSubnetId : ''
    privateDnsZoneIds: privateNetworking ? network!.outputs.aiDnsZoneIds : []
  }
}

module containerEnvironment 'modules/container-env.bicep' = {
  scope: rg
  name: 'container-environment'
  params: {
    location: location
    tags: allTags
    name: names.containerEnvironment
    logAnalyticsWorkspaceName: deployMonitoring ? monitoring!.outputs.workspaceName : ''
    infrastructureSubnetId: privateNetworking ? network!.outputs.appsSubnetId : ''
  }
}

module apps 'modules/apps.bicep' = if (deployApps) {
  scope: rg
  name: 'apps'
  params: {
    location: location
    tags: allTags
    environmentId: containerEnvironment.outputs.id
    environmentDefaultDomain: containerEnvironment.outputs.defaultDomain
    registryServer: registry.outputs.loginServer
    imageTag: imageTag
    appsIdentityId: identity.outputs.appsId
    appsIdentityClientId: identity.outputs.appsClientId
    aiIdentityId: identity.outputs.aiId
    aiIdentityClientId: identity.outputs.aiClientId
    connectionStringSecretUri: keyVault.outputs.connectionStringSecretUri
    inboundSecretUri: keyVault.outputs.inboundSecretUri
    appInsightsConnectionString: deployMonitoring ? monitoring!.outputs.appInsightsConnectionString : ''
    deploySimulators: deploySimulators
    deployAi: deployAi
    exposeApi: exposeApi
    exposeSimulators: exposeSimulators
    allowedIpRanges: allowedIpRanges
    demoTiming: demoTiming
    migrateOnStartup: migrateOnStartup
    notificationsTimeZone: notificationsTimeZone
    apiAiProvider: apiAiProvider
    apiExtraEnv: apiExtraEnv
    webMaxReplicas: webMaxReplicas
    aiProjectEndpoint: deployAi ? ai!.outputs.projectEndpoint : ''
    aiAccountEndpoint: deployAi ? ai!.outputs.accountEndpoint : ''
    aiModelDeploymentName: deployAi ? ai!.outputs.deploymentName : ''
    aiInferenceEnabled: aiInferenceEnabled
    aiPromptShieldsAttested: aiPromptShieldsAttested
  }
}

// ---------------------------------------------------------------------------------------------------
// Outputs (read by scripts/deploy.sh)
// ---------------------------------------------------------------------------------------------------

output resourceGroupName string = rg.name
output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output keyVaultName string = keyVault.outputs.name
output postgresServerName string = postgres.outputs.name
output deploySimulators bool = deploySimulators
output deployAi bool = deployAi
output webUrl string = deployApps ? apps!.outputs.webUrl : ''
output apiUrl string = deployApps ? apps!.outputs.apiUrl : ''
output hisSimulatorUrl string = deployApps ? apps!.outputs.hisSimulatorUrl : ''
output messagingSimulatorUrl string = deployApps ? apps!.outputs.messagingSimulatorUrl : ''
