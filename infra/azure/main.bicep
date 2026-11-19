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

@description('Azure region for every resource.')
param location string = deployment().location

@description('Lower-case letters and digits only. Prefix of every resource name.')
@minLength(2)
@maxLength(8)
param namePrefix string = 'careloop'

@description('Extra tags for every resource.')
param tags object = {}

@description('Deploy into this existing resource group instead of creating rg-<prefix>-<environment>. The group is left as it is: not created, moved, retagged or, on teardown, deleted. Empty creates the group.')
param resourceGroupName string = ''

// ---------------------------------------------------------------------------------------------------
// Switches
// ---------------------------------------------------------------------------------------------------

@description('false: public endpoints protected by firewall rules and Entra ID. true: a virtual network, with private endpoints for PostgreSQL, Key Vault and the registry. Use true before any real patient data.')
param privateNetworking bool = false

@description('Deploy the HIS and messaging simulators, so the environment can run the demo scenarios.')
param deploySimulators bool = true

@description('Deploy a Log Analytics workspace and Application Insights, and send container logs there.')
param deployMonitoring bool = true

// ---------------------------------------------------------------------------------------------------
// Application
// ---------------------------------------------------------------------------------------------------

@description('Image tag to run. Empty deploys the platform only (pass 1); scripts/deploy.sh sets it for pass 2.')
param imageTag string = ''

@description('Give the API its own public address, so a hospital system can post to /fhir. false: reachable only from the other apps, and from outside through the web app on /api/.')
param exposeApi bool = true

@description('Give the simulators public addresses so their screens can be opened in a browser. false: reachable only from the other apps.')
param exposeSimulators bool = true

@description('CIDR ranges allowed to reach the public apps. Empty, the default, allows everyone. Not yet tried with a list: a list on an app may also block calls from the other apps, so test before relying on it.')
param allowedIpRanges array = []

@description('Use the demo pathway (deadlines divided by 1800) and a one-second deadline sweep. Must be true wherever the simulators run scenarios, false for real use.')
param demoTiming bool = deploySimulators

@description('Apply EF Core migrations when the API starts. Nothing else in this project applies them.')
param migrateOnStartup bool = true

@description('The API setting Ai:Provider. NoOp keeps AI off; Foundry has the API call a model in the Foundry project (aiProjectEndpoint, aiModelDeploymentName) for the role briefing, the impact summary and the patient message draft.')
@allowed(['NoOp', 'Foundry'])
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

@description('true: give the managed identity its access with Azure role assignments, which needs Owner or Role Based Access Control Administrator. false: for a deployer with Contributor only. The apps then pull images with the registry admin password, Key Vault uses access policies instead of roles, and the Foundry owner must grant any needed model access by hand.')
param assignRoles bool = true

@description('Object ID of the person or service principal deploying. When set, it can read and write the Key Vault secrets, which lets scripts/deploy.sh reuse them on the next run.')
param deployerPrincipalId string = ''

// ---------------------------------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------------------------------

@description('Region of the PostgreSQL server. Some subscriptions are refused PostgreSQL in some regions; put only the database elsewhere with this.')
param postgresLocation string = location

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
// Foundry: the API calls a model deployment of a Foundry project with its own managed identity when
// apiAiProvider is Foundry. The API remains on NoOp by default.
// ---------------------------------------------------------------------------------------------------

@description('Endpoint of an existing Foundry project, https://<account>.services.ai.azure.com/api/projects/<project>. Never write the value in a parameter file; it is read from the environment. Ignored when createFoundry is true.')
param aiProjectEndpoint string = ''

@description('Name of the model deployment in the existing project that writes the texts. Ignored when createFoundry is true.')
param aiModelDeploymentName string = ''

@description('Resource ID of the existing Foundry account, /subscriptions/.../providers/Microsoft.CognitiveServices/accounts/<name>. When set (and assignRoles is true), the API identity is given the Foundry User role on it. Empty: someone who owns the account grants the role by hand.')
param aiFoundryAccountId string = ''

@description('Create a Foundry account, project, model deployment and Prompt Shields guardrail in this environment, for a subscription that has no Foundry to use. false, the default, creates nothing.')
param createFoundry bool = false

@description('Region of the created Foundry account. The model must be offered there; check data residency.')
param foundryLocation string = location

@description('Model to deploy in the created Foundry. Check that it is offered in foundryLocation and that the subscription has quota.')
param foundryModelName string = 'gpt-4.1-mini'
param foundryModelVersion string = '2025-04-14'

@description('GlobalStandard may process prompts in any Azure region. Use DataZoneStandard or Standard where residency matters.')
param foundryModelSku string = 'GlobalStandard'

@description('Thousands of tokens per minute.')
param foundryModelCapacity int = 10

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
  // Registry and Key Vault names are global and short, so they carry the token instead of the environment.
  registry: 'cr${namePrefix}${token}'
  keyVault: 'kv-${namePrefix}-${take(token, 10)}'
  // The region is in the name so that moving the database to another region makes a new server
  // beside the old one, rather than trying to move a server, which Azure cannot do.
  postgres: 'psql-${baseName}-${postgresLocation}-${take(token, 6)}'
  foundryAccount: 'ai-${baseName}-${token}'
  foundryProject: 'careloop'
  containerEnvironment: 'cae-${baseName}'
}

var deployApps = !empty(imageTag)
var useExistingGroup = !empty(resourceGroupName)
var targetGroupName = useExistingGroup ? resourceGroupName : names.resourceGroup

// ---------------------------------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------------------------------

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = if (!useExistingGroup) {
  name: names.resourceGroup
  location: location
  tags: allTags
}

module monitoring 'modules/monitoring.bicep' = if (deployMonitoring) {
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
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
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'network'
  params: {
    location: location
    tags: allTags
    name: names.virtualNetwork
  }
}

module identity 'modules/identity.bicep' = {
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'identity'
  params: {
    location: location
    tags: allTags
    appsIdentityName: names.appsIdentity
  }
}

module registry 'modules/registry.bicep' = {
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'registry'
  params: {
    location: location
    tags: allTags
    name: names.registry
    pullPrincipalIds: [identity.outputs.appsPrincipalId]
    assignRoles: assignRoles
    publicAccess: !privateNetworking || registryPublicAccess
    privateEndpointSubnetId: privateNetworking ? network!.outputs.privateEndpointSubnetId : ''
    privateDnsZoneId: privateNetworking ? network!.outputs.registryDnsZoneId : ''
  }
}

module postgres 'modules/postgres.bicep' = {
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'postgres'
  params: {
    location: postgresLocation
    privateEndpointLocation: location
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
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'keyvault'
  params: {
    location: location
    tags: allTags
    name: names.keyVault
    purgeProtection: keyVaultPurgeProtection
    readerPrincipalId: identity.outputs.appsPrincipalId
    deployerPrincipalId: deployerPrincipalId
    assignRoles: assignRoles
    postgresAdminPassword: postgresAdminPassword
    connectionString: 'Host=${postgres.outputs.fqdn};Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=${postgresSslMode}'
    simulatorInboundSecret: simulatorInboundSecret
    privateEndpointSubnetId: privateNetworking ? network!.outputs.privateEndpointSubnetId : ''
    privateDnsZoneId: privateNetworking ? network!.outputs.keyVaultDnsZoneId : ''
  }
}

var useCreatedFoundry = createFoundry

module foundry 'modules/foundry.bicep' = if (useCreatedFoundry) {
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'foundry'
  params: {
    location: foundryLocation
    tags: allTags
    accountName: names.foundryAccount
    projectName: names.foundryProject
    modelName: foundryModelName
    modelVersion: foundryModelVersion
    modelSku: foundryModelSku
    modelCapacity: foundryModelCapacity
    userPrincipalIds: assignRoles ? [identity.outputs.appsPrincipalId] : []
  }
}

var foundryIdParts = split(aiFoundryAccountId, '/')

module foundryAccess 'modules/foundry-access.bicep' = if (assignRoles && !createFoundry && !empty(aiFoundryAccountId)) {
  scope: resourceGroup(foundryIdParts[?2] ?? subscription().subscriptionId, foundryIdParts[?4] ?? targetGroupName)
  name: 'foundry-access-${environmentName}'
  params: {
    accountName: last(foundryIdParts)!
    principalId: identity.outputs.appsPrincipalId
  }
}

module containerEnvironment 'modules/container-env.bicep' = {
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
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
  scope: resourceGroup(targetGroupName)
  dependsOn: [rg]
  name: 'apps'
  params: {
    location: location
    tags: allTags
    environmentId: containerEnvironment.outputs.id
    environmentDefaultDomain: containerEnvironment.outputs.defaultDomain
    registryServer: registry.outputs.loginServer
    registryName: registry.outputs.name
    assignRoles: assignRoles
    imageTag: imageTag
    appsIdentityId: identity.outputs.appsId
    appsIdentityClientId: identity.outputs.appsClientId
    connectionStringSecretUri: keyVault.outputs.connectionStringSecretUri
    inboundSecretUri: keyVault.outputs.inboundSecretUri
    appInsightsConnectionString: deployMonitoring ? monitoring!.outputs.appInsightsConnectionString : ''
    deploySimulators: deploySimulators
    exposeApi: exposeApi
    exposeSimulators: exposeSimulators
    allowedIpRanges: allowedIpRanges
    demoTiming: demoTiming
    migrateOnStartup: migrateOnStartup
    notificationsTimeZone: notificationsTimeZone
    apiAiProvider: apiAiProvider
    apiExtraEnv: apiExtraEnv
    webMaxReplicas: webMaxReplicas
    aiProjectEndpoint: useCreatedFoundry ? foundry!.outputs.projectEndpoint : aiProjectEndpoint
    aiModelDeploymentName: useCreatedFoundry ? foundry!.outputs.deploymentName : aiModelDeploymentName
  }
}

// ---------------------------------------------------------------------------------------------------
// Outputs (read by scripts/deploy.sh)
// ---------------------------------------------------------------------------------------------------

output resourceGroupName string = targetGroupName
output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output keyVaultName string = keyVault.outputs.name
output postgresServerName string = postgres.outputs.name
output deploySimulators bool = deploySimulators
output webUrl string = deployApps ? apps!.outputs.webUrl : ''
output apiUrl string = deployApps ? apps!.outputs.apiUrl : ''
output hisSimulatorUrl string = deployApps ? apps!.outputs.hisSimulatorUrl : ''
output messagingSimulatorUrl string = deployApps ? apps!.outputs.messagingSimulatorUrl : ''
