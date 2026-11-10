targetScope = 'resourceGroup'

param location string = resourceGroup().location
param appName string
param planName string

@minLength(3)
@maxLength(24)
param keyVaultName string

param registryName string = 'acrintranet${uniqueString(resourceGroup().id)}'
param imageRepository string = 'intranet'

@minLength(1)
@maxLength(100)
param applicationName string = 'Intranet'

@minLength(1)
@maxLength(100)
param companyName string = 'Mi empresa'

@description('Git commit SHA tag. The image must already exist in ACR.')
@minLength(1)
param imageTag string

@allowed([
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1v3'
  'P2v3'
  'P3v3'
])
param planSku string = 'B1'

@description('Optional existing Key Vault secret holding the Azure Blob attachment connection string.')
param attachmentConnectionSecretName string = ''

module containerRegistry './registry.bicep' = {
  name: 'container-registry'
  params: {
    location: location
    registryName: registryName
  }
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-runtime'
  location: location
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    containerRegistry
  ]
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    accessPolicies: []
    publicNetworkAccess: 'Enabled'
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: planSku
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${pullIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    keyVaultReferenceIdentity: pullIdentity.id
    httpsOnly: true
    clientAffinityEnabled: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerRegistry.outputs.loginServer}/${imageRepository}:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: pullIdentity.properties.clientId
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      webSocketsEnabled: true
      healthCheckPath: '/health'
    }
  }
  dependsOn: [
    acrPull
  ]
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, pullIdentity.id, 'KeyVaultSecretsUser')
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource settings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: app
  name: 'appsettings'
  properties: union({
    ASPNETCORE_ENVIRONMENT: 'Production'
    Intranet__ApplicationName: applicationName
    Intranet__CompanyName: companyName
    DataProtection__ApplicationName: appName
    ASPNETCORE_HTTP_PORTS: '8080'
    // App Service terminates TLS and only exposes the container through its proxy.
    ASPNETCORE_FORWARDEDHEADERS_ENABLED: 'true'
    WEBSITES_PORT: '8080'
    WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'true'
    ConnectionStrings__Default: '@Microsoft.KeyVault(SecretUri=${vault.properties.vaultUri}secrets/ConnectionStrings--Default/)'
    ApplicationInsights__ConnectionString: insights.properties.ConnectionString
    Messaging__AttachmentStorage__LocalRootPath: '/home/.messaging-attachments'
  }, empty(attachmentConnectionSecretName) ? {} : {
    Messaging__AttachmentStorage__ConnectionString: '@Microsoft.KeyVault(SecretUri=${vault.properties.vaultUri}secrets/${attachmentConnectionSecretName}/)'
  })
  dependsOn: [
    secretsUser
  ]
}

resource ftpPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: app
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scmPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: app
  name: 'scm'
  properties: {
    allow: false
  }
}

output appName string = app.name
output appUrl string = 'https://${app.properties.defaultHostName}'
output keyVaultName string = vault.name
output runtimePrincipalId string = pullIdentity.properties.principalId
output image string = '${containerRegistry.outputs.loginServer}/${imageRepository}:${imageTag}'
