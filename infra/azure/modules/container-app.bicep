// One container app with HTTP ingress, a user-assigned identity and health probes.
param location string
param tags object
param name string
param environmentId string
param identityId string
param registryServer string

@description('Empty: pull with the managed identity. Set: pull as this registry user with registryPassword.')
param registryUsername string = ''

@secure()
param registryPassword string = ''
param image string
param targetPort int

@description('true: reachable from the internet. false: reachable only from other apps in the environment.')
param external bool = false

@description('CIDR ranges allowed in when external. Empty allows everyone.')
param allowedIpRanges array = []

@description('{ name, value } or { name, secretRef } pairs.')
param env array = []

@description('{ name, value } or { name, keyVaultUrl, identity } entries.')
param secrets array = []

param volumes array = []
param volumeMounts array = []
param cpu string = '0.5'
param memory string = '1Gi'
param minReplicas int = 1
param maxReplicas int = 1
param healthPath string = '/health'

var probe = {
  httpGet: {
    path: healthPath
    port: targetPort
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: external
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
        ipSecurityRestrictions: external && !empty(allowedIpRanges)
          ? map(allowedIpRanges, (range, i) => {
              name: 'allow-${i}'
              action: 'Allow'
              ipAddressRange: range
            })
          : null
      }
      registries: [
        empty(registryUsername)
          ? { server: registryServer, identity: identityId }
          : { server: registryServer, username: registryUsername, passwordSecretRef: 'registry-password' }
      ]
      secrets: empty(registryUsername)
        ? secrets
        : concat(secrets, [{ name: 'registry-password', value: registryPassword }])
    }
    template: {
      containers: [
        {
          name: name
          image: image
          env: env
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          volumeMounts: volumeMounts
          probes: [
            // Up to two and a half minutes to start: the API applies its migrations first.
            union(probe, { type: 'Startup', periodSeconds: 5, failureThreshold: 30 })
            union(probe, { type: 'Liveness', periodSeconds: 30, failureThreshold: 3 })
            union(probe, { type: 'Readiness', periodSeconds: 10, failureThreshold: 3 })
          ]
        }
      ]
      volumes: volumes
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
