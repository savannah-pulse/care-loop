// Production shape: private networking, no simulators, real pathway timing, a database sized for
// steady load with geo-redundant backups, and Key Vault purge protection.
//
// This file sets the infrastructure up for real use; it does not make the prototype fit for real
// patient data. See "Before real patient data" in ../README.md and the prerequisites in AGENTS.md.
using '../main.bicep'

param environmentName = 'prod'
param location = readEnvironmentVariable('AZURE_LOCATION', 'uaenorth')
param namePrefix = readEnvironmentVariable('NAME_PREFIX', 'careloop')
// Empty creates rg-<prefix>-<environment>; a name deploys into that existing resource group.
param resourceGroupName = readEnvironmentVariable('RESOURCE_GROUP', '')
// The database can sit in another region when the subscription is refused PostgreSQL in the main one.
param postgresLocation = readEnvironmentVariable('POSTGRES_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'uaenorth'))

param privateNetworking = true
param deploySimulators = false
param demoTiming = false
param deployMonitoring = true
param logRetentionDays = 90
param keyVaultPurgeProtection = true

// With private networking the registry needs the Premium tier. It stays reachable from the
// internet (Entra ID sign-in required) so the Deploy workflow can push; set false once images are
// pushed from a runner inside the virtual network.
param registryPublicAccess = true

// The dashboard and the API are public, as in dev. Before real use, set exposeApi = false or an
// allowedIpRanges list (see "What is reachable from the internet" in ../README.md).

param postgresSkuName = 'Standard_D2ds_v5'
param postgresSkuTier = 'GeneralPurpose'
param postgresStorageGb = 64
param postgresBackupRetentionDays = 35
param postgresGeoRedundantBackup = true
// 'ZoneRedundant' where the region has availability zones and the cost is accepted.
param postgresHighAvailability = 'Disabled'

param apiAiProvider = 'NoOp'

// Set by scripts/deploy.sh and the Deploy workflow. Never write a value here.
param imageTag = readEnvironmentVariable('IMAGE_TAG', '')
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param simulatorInboundSecret = readEnvironmentVariable('SIMULATOR_INBOUND_SECRET')
param deployerPrincipalId = readEnvironmentVariable('DEPLOYER_PRINCIPAL_ID', '')
// false for a deployer with Contributor only; see "Deploying without the right to assign roles" in ../README.md.
param assignRoles = toLower(readEnvironmentVariable('ASSIGN_ROLES', 'true')) == 'true'
