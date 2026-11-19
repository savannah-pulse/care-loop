// The CareLoop container apps and the settings that wire them together. This is the Azure
// counterpart of docker-compose.yml; keep the two in step when a service gains a setting.
//
// Apps call each other over HTTPS on <app>.internal.<environment domain> (or <app>.<environment
// domain> when the app is public). The addresses are built from names rather than read from the
// apps, because the API and the simulators point at each other.
param location string
param tags object
param environmentId string
param environmentDefaultDomain string
param registryServer string
param registryName string

@description('false: no role lets the identities pull images, so the apps pull with the registry admin password.')
param assignRoles bool
param imageTag string
param appsIdentityId string
param appsIdentityClientId string
param connectionStringSecretUri string
param inboundSecretUri string
param appInsightsConnectionString string
param deploySimulators bool
param exposeApi bool
param exposeSimulators bool
param allowedIpRanges array
param demoTiming bool
param migrateOnStartup bool
param notificationsTimeZone string
param apiAiProvider string
param apiExtraEnv array
param webMaxReplicas int
param aiProjectEndpoint string

var appNames = {
  api: 'api'
  web: 'web'
  hisSimulator: 'his-simulator'
  messagingSimulator: 'messaging-simulator'
}

var internalDomain = 'internal.${environmentDefaultDomain}'
var apiHost = '${appNames.api}.${exposeApi ? environmentDefaultDomain : internalDomain}'
var hisSimulatorHost = '${appNames.hisSimulator}.${exposeSimulators ? environmentDefaultDomain : internalDomain}'
var messagingSimulatorHost = '${appNames.messagingSimulator}.${exposeSimulators ? environmentDefaultDomain : internalDomain}'

var images = {
  api: '${registryServer}/careloop/api:${imageTag}'
  web: '${registryServer}/careloop/web:${imageTag}'
  hisSimulator: '${registryServer}/careloop/his-simulator:${imageTag}'
  messagingSimulator: '${registryServer}/careloop/messaging-simulator:${imageTag}'
}

// Without role assignments the apps cannot pull with their identity, so each is given the
// registry's admin password, which it keeps as a secret of its own.
resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}
var registryUsername = assignRoles ? '' : registryName

var connectionStringSecret = {
  name: 'careloop-connection-string'
  keyVaultUrl: connectionStringSecretUri
  identity: appsIdentityId
}
var inboundSecret = {
  name: 'simulator-inbound-secret'
  keyVaultUrl: inboundSecretUri
  identity: appsIdentityId
}

var telemetryEnv = empty(appInsightsConnectionString)
  ? []
  : [{ name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }]

var dotnetEnv = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
  // The environment's ingress ends TLS; trust its X-Forwarded-* headers.
  { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
]

// --- API --------------------------------------------------------------------------------------------

var apiBaseEnv = [
  { name: 'ConnectionStrings__CareLoop', secretRef: connectionStringSecret.name }
  { name: 'Database__MigrateOnStartup', value: string(migrateOnStartup) }
  { name: 'Notifications__Sms__InboundSecret', secretRef: inboundSecret.name }
  { name: 'Notifications__Email__InboundSecret', secretRef: inboundSecret.name }
  { name: 'Notifications__TimeZone', value: notificationsTimeZone }
  { name: 'Ai__Provider', value: apiAiProvider }
  // Lets DefaultAzureCredential pick the user-assigned identity.
  { name: 'AZURE_CLIENT_ID', value: appsIdentityClientId }
]

// The demo pathway's deadlines last seconds, so the sweep must run about once a second.
var apiDemoEnv = demoTiming
  ? [
      { name: 'Pathways__Directory', value: 'Infrastructure/Pathways/demo' }
      { name: 'DeadlineWorker__Interval', value: '00:00:01' }
    ]
  : []

// Text messages and emails both go to the messaging simulator. Staff addresses and facility names
// are the synthetic ones from docker-compose.yml.
var apiSimulatorEnv = deploySimulators
  ? [
      { name: 'ExternalSystems__Systems__his-simulator__BaseUrl', value: 'https://${hisSimulatorHost}' }
      { name: 'Notifications__Sms__BaseUrl', value: 'https://${messagingSimulatorHost}' }
      { name: 'Notifications__Email__BaseUrl', value: 'https://${messagingSimulatorHost}' }
      { name: 'Notifications__FacilityNames__family-health-centre', value: 'Family Health Centre' }
      { name: 'Notifications__FacilityNames__general-hospital', value: 'General Hospital' }
      { name: 'Notifications__FacilityNames__regional-hospital', value: 'Regional Hospital' }
      { name: 'Notifications__FacilityContacts__family-health-centre', value: 'SYN-PHONE-FAMILY-HEALTH-CENTRE' }
      { name: 'Notifications__FacilityContacts__general-hospital', value: 'SYN-PHONE-GENERAL-HOSPITAL' }
      { name: 'Notifications__FacilityContacts__regional-hospital', value: 'SYN-PHONE-REGIONAL-HOSPITAL' }
      { name: 'Notifications__StaffContacts__nurse-navigator__Channel', value: 'Email' }
      { name: 'Notifications__StaffContacts__nurse-navigator__Address', value: 'SYN-EMAIL-NURSE-NAVIGATORS' }
      { name: 'Notifications__StaffContacts__clinician__Channel', value: 'Email' }
      { name: 'Notifications__StaffContacts__clinician__Address', value: 'SYN-EMAIL-CLINICIANS' }
      { name: 'Notifications__StaffContacts__receiving-clinician__Channel', value: 'Email' }
      { name: 'Notifications__StaffContacts__receiving-clinician__Address', value: 'SYN-EMAIL-RECEIVING-CLINICIANS' }
      { name: 'Notifications__StaffContacts__lab__Channel', value: 'Email' }
      { name: 'Notifications__StaffContacts__lab__Address', value: 'SYN-EMAIL-LAB' }
      { name: 'Notifications__StaffContacts__facility-lead__Channel', value: 'Email' }
      { name: 'Notifications__StaffContacts__facility-lead__Address', value: 'SYN-EMAIL-FACILITY-LEADS' }
      { name: 'Notifications__StaffContacts__coordinator__Channel', value: 'Email' }
      { name: 'Notifications__StaffContacts__coordinator__Address', value: 'SYN-EMAIL-COORDINATORS' }
    ]
  : []

// The API asks the project's agents (role-briefing, impact-summary, patient-notification) with the apps
// identity (AZURE_CLIENT_ID above), which needs Foundry User on the Foundry account. The agents and their
// models are owned in the Foundry project, not here.
var apiFoundryEnv = empty(aiProjectEndpoint)
  ? []
  : [
      { name: 'Ai__Foundry__ProjectEndpoint', value: aiProjectEndpoint }
    ]

module api 'container-app.bicep' = {
  name: 'app-api'
  params: {
    location: location
    tags: tags
    name: appNames.api
    environmentId: environmentId
    identityId: appsIdentityId
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: assignRoles ? '' : registry.listCredentials().passwords[0].value
    image: images.api
    targetPort: 8080
    external: exposeApi
    allowedIpRanges: allowedIpRanges
    secrets: [connectionStringSecret, inboundSecret]
    env: concat(dotnetEnv, apiBaseEnv, apiDemoEnv, apiSimulatorEnv, apiFoundryEnv, telemetryEnv, apiExtraEnv)
    // One replica exactly: the deadline worker, the outbox and the in-memory notification send log
    // live in this process, and migrations run at startup.
    minReplicas: 1
    maxReplicas: 1
  }
}

// --- Web --------------------------------------------------------------------------------------------

// The web image's own nginx configuration (src/web/nginx.conf) forwards /api/ to http://api:8080,
// the Docker Compose address. In Container Apps the API is reached over HTTPS on its own host name
// and the environment routes by Host header, so those two lines are rewritten here and the result
// is mounted over the image's copy. There is one source file; if its proxy lines change shape the
// deployment stops with the message below rather than shipping a proxy that points nowhere.
var nginxSource = loadTextContent('../../../src/web/nginx.conf')
var nginxUpstream = 'proxy_pass http://api:8080/;'
var nginxHostHeader = 'proxy_set_header Host $host;'
var webNginxConf = contains(nginxSource, nginxUpstream) && contains(nginxSource, nginxHostHeader)
  ? replace(
      replace(nginxSource, nginxUpstream, 'proxy_pass https://${apiHost}/;\n        proxy_ssl_server_name on;'),
      nginxHostHeader,
      'proxy_set_header Host ${apiHost};'
    )
  : fail('src/web/nginx.conf no longer contains "${nginxUpstream}" and "${nginxHostHeader}". Update nginxUpstream and nginxHostHeader in infra/azure/modules/apps.bicep to match.')

module web 'container-app.bicep' = {
  name: 'app-web'
  params: {
    location: location
    tags: tags
    name: appNames.web
    environmentId: environmentId
    identityId: appsIdentityId
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: assignRoles ? '' : registry.listCredentials().passwords[0].value
    image: images.web
    targetPort: 80
    external: true
    allowedIpRanges: allowedIpRanges
    healthPath: '/'
    cpu: '0.25'
    memory: '0.5Gi'
    minReplicas: 1
    maxReplicas: webMaxReplicas
    // The nginx configuration is not a secret; a secret volume is simply how Container Apps mounts a file.
    secrets: [{ name: 'nginx-conf', value: webNginxConf }]
    volumes: [
      {
        name: 'nginx-conf'
        storageType: 'Secret'
        secrets: [{ secretRef: 'nginx-conf', path: 'default.conf' }]
      }
    ]
    volumeMounts: [
      { volumeName: 'nginx-conf', mountPath: '/etc/nginx/conf.d' }
    ]
  }
  // nginx resolves the API's host name once, when it starts.
  dependsOn: [api]
}

// --- Simulators -------------------------------------------------------------------------------------

// One replica each: a simulator keeps its state in memory.
module hisSimulator 'container-app.bicep' = if (deploySimulators) {
  name: 'app-his-simulator'
  params: {
    location: location
    tags: tags
    name: appNames.hisSimulator
    environmentId: environmentId
    identityId: appsIdentityId
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: assignRoles ? '' : registry.listCredentials().passwords[0].value
    image: images.hisSimulator
    targetPort: 8080
    external: exposeSimulators
    allowedIpRanges: allowedIpRanges
    env: concat(dotnetEnv, [
      { name: 'CareLoop__FhirBaseUrl', value: 'https://${apiHost}/fhir' }
      { name: 'Contacts__PatientChannel', value: 'sms' }
      { name: 'Scenarios__Directory', value: '/app/scenarios' }
      { name: 'Scenarios__RealSecondsPerScenarioHour', value: '2' }
    ])
  }
}

module messagingSimulator 'container-app.bicep' = if (deploySimulators) {
  name: 'app-messaging-simulator'
  params: {
    location: location
    tags: tags
    name: appNames.messagingSimulator
    environmentId: environmentId
    identityId: appsIdentityId
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: assignRoles ? '' : registry.listCredentials().passwords[0].value
    image: images.messagingSimulator
    targetPort: 8080
    external: exposeSimulators
    allowedIpRanges: allowedIpRanges
    secrets: [inboundSecret]
    env: concat(dotnetEnv, [
      { name: 'CareLoop__CallbackBaseUrl', value: 'https://${apiHost}/notifications/inbound' }
      { name: 'CareLoop__InboundSecret', secretRef: inboundSecret.name }
      { name: 'Scenarios__Directory', value: '/app/scenarios' }
      { name: 'Scenarios__RealSecondsPerScenarioHour', value: '2' }
    ])
  }
}

output webUrl string = 'https://${web.outputs.fqdn}'
output apiUrl string = exposeApi ? 'https://${apiHost}' : 'https://${web.outputs.fqdn}/api'
output hisSimulatorUrl string = deploySimulators && exposeSimulators ? 'https://${hisSimulatorHost}' : ''
output messagingSimulatorUrl string = deploySimulators && exposeSimulators ? 'https://${messagingSimulatorHost}' : ''
