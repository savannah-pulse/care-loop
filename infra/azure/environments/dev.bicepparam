// Demo and development: public endpoints, both simulators, demo timing, smallest sizes.
// Synthetic data only. Copy this file to add an environment; the file name is the environment name
// passed to scripts/deploy.sh and to the Deploy workflow.
using '../main.bicep'

param environmentName = 'dev'
param location = readEnvironmentVariable('AZURE_LOCATION', 'uaenorth')
param namePrefix = readEnvironmentVariable('NAME_PREFIX', 'careloop')

param privateNetworking = false
param deploySimulators = true
param deployMonitoring = true

// The AI service and the Foundry resources are created, but inference stays off and the API keeps
// Ai:Provider = NoOp until WS9 is finished. Sweden Central offers the model; change it if the model
// is available nearer, and settle data residency before anything but synthetic data is sent.
param deployAi = true
param aiLocation = readEnvironmentVariable('AZURE_AI_LOCATION', 'swedencentral')
param aiInferenceEnabled = false
param aiPromptShieldsAttested = false
param apiAiProvider = 'NoOp'

// Set by scripts/deploy.sh and the Deploy workflow. Never write a value here.
param imageTag = readEnvironmentVariable('IMAGE_TAG', '')
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param simulatorInboundSecret = readEnvironmentVariable('SIMULATOR_INBOUND_SECRET')
param deployerPrincipalId = readEnvironmentVariable('DEPLOYER_PRINCIPAL_ID', '')
