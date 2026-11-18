// Container Apps environment (workload profiles, Consumption only). With a subnet it runs inside the
// virtual network, which lets the apps reach the private endpoints; its ingress stays public.
param location string
param tags object
param name string
param logAnalyticsWorkspaceName string
param infrastructureSubnetId string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = if (!empty(logAnalyticsWorkspaceName)) {
  name: logAnalyticsWorkspaceName
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    workloadProfiles: [
      { name: 'Consumption', workloadProfileType: 'Consumption' }
    ]
    appLogsConfiguration: empty(logAnalyticsWorkspaceName)
      ? null
      : {
          destination: 'log-analytics'
          logAnalyticsConfiguration: {
            customerId: workspace!.properties.customerId
            sharedKey: workspace!.listKeys().primarySharedKey
          }
        }
    vnetConfiguration: empty(infrastructureSubnetId)
      ? null
      : {
          infrastructureSubnetId: infrastructureSubnetId
          internal: false
        }
  }
}

output id string = environment.id
output defaultDomain string = environment.properties.defaultDomain
