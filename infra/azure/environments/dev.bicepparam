// Demo and development: public endpoints, both simulators, demo timing, smallest sizes.
// Synthetic data only. Copy this file to add an environment; the file name is the environment name
// passed to scripts/deploy.sh and to the Deploy workflow.
using '../main.bicep'

param environmentName = 'dev'
param location = readEnvironmentVariable('AZURE_LOCATION', 'uaenorth')
param namePrefix = readEnvironmentVariable('NAME_PREFIX', 'careloop')
// Empty creates rg-<prefix>-<environment>; a name deploys into that existing resource group.
param resourceGroupName = readEnvironmentVariable('RESOURCE_GROUP', '')
// The database can sit in another region when the subscription is refused PostgreSQL in the main one.
param postgresLocation = readEnvironmentVariable('POSTGRES_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'uaenorth'))
// Smallest size by default. Visual Studio subscriptions are refused the Burstable tier (the error is
// "Specified SKU is not supported in this region"): use Standard_D2ds_v5 and GeneralPurpose there.
param postgresSkuName = readEnvironmentVariable('POSTGRES_SKU_NAME', 'Standard_B1ms')
param postgresSkuTier = readEnvironmentVariable('POSTGRES_SKU_TIER', 'Burstable')

param privateNetworking = false
param deploySimulators = true
param deployMonitoring = true

// Demo timing: the demo pathway (deadlines in seconds) and a one-second deadline sweep, to match the
// simulators' scenarios. Set DEMO_TIMING=false to run the real pathway, whose deadlines last hours and
// days, so that open cases stay on time long enough to be looked at (see infra/demo/README.md).
param demoTiming = toLower(readEnvironmentVariable('DEMO_TIMING', 'true')) == 'true'

// Foundry: the project whose agents the API asks. AI stays off (NoOp) unless API_AI_PROVIDER=Foundry
// is set for the deployment and AZURE_AI_PROJECT_ENDPOINT names the project.
param aiProjectEndpoint = readEnvironmentVariable('AZURE_AI_PROJECT_ENDPOINT', '')
param aiFoundryAccountId = readEnvironmentVariable('AZURE_AI_FOUNDRY_ACCOUNT_ID', '')
param createFoundry = toLower(readEnvironmentVariable('CREATE_FOUNDRY', 'false')) == 'true'
param foundryLocation = readEnvironmentVariable('AZURE_AI_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'uaenorth'))
param apiAiProvider = readEnvironmentVariable('API_AI_PROVIDER', 'NoOp')

// Set by scripts/deploy.sh and the Deploy workflow. Never write a value here.
param imageTag = readEnvironmentVariable('IMAGE_TAG', '')
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param simulatorInboundSecret = readEnvironmentVariable('SIMULATOR_INBOUND_SECRET')
param deployerPrincipalId = readEnvironmentVariable('DEPLOYER_PRINCIPAL_ID', '')
// false for a deployer with Contributor only; see "Deploying without the right to assign roles" in ../README.md.
param assignRoles = toLower(readEnvironmentVariable('ASSIGN_ROLES', 'true')) == 'true'
