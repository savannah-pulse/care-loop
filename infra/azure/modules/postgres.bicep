// Azure Database for PostgreSQL flexible server and the CareLoop database.
// Public mode: reachable from Azure services only (the Container Apps outbound addresses are not fixed).
// Private mode: no public access; reached through a private endpoint.
param location string
param privateEndpointLocation string
param tags object
param name string
param version string
param administratorLogin string
@secure()
param administratorPassword string
param databaseName string
param skuName string
param skuTier string
param storageGb int
param backupRetentionDays int
param geoRedundantBackup bool
param highAvailability string

@description('Availability zone to place the server in, for example 1. Empty lets Azure choose.')
param availabilityZone string = ''
param privateEndpointSubnetId string
param privateDnsZoneId string

var usePrivateEndpoint = !empty(privateEndpointSubnetId)

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    version: version
    availabilityZone: empty(availabilityZone) ? null : availabilityZone
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    authConfig: {
      // The API signs in with a password today; Entra ID sign-in needs a change in the API first.
      passwordAuth: 'Enabled'
      activeDirectoryAuth: 'Disabled'
    }
    storage: {
      storageSizeGB: storageGb
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: geoRedundantBackup ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: highAvailability
    }
    network: {
      publicNetworkAccess: usePrivateEndpoint ? 'Disabled' : 'Enabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// The 0.0.0.0 rule is Azure's "allow Azure services" switch. It admits any Azure tenant's services to
// the sign-in prompt, so it suits synthetic data only; use private networking for anything real.
resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (!usePrivateEndpoint) {
  parent: server
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

module privateEndpoint 'private-endpoint.bicep' = if (usePrivateEndpoint) {
  name: 'postgres-private-endpoint'
  params: {
    location: privateEndpointLocation
    tags: tags
    name: 'pe-${name}'
    subnetId: privateEndpointSubnetId
    targetResourceId: server.id
    groupId: 'postgresqlServer'
    privateDnsZoneIds: [privateDnsZoneId]
  }
}

output name string = server.name
output fqdn string = server.properties.fullyQualifiedDomainName
