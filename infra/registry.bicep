targetScope = 'resourceGroup'

param location string = resourceGroup().location

@minLength(5)
@maxLength(50)
param registryName string = 'acrintranet${uniqueString(resourceGroup().id)}'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

output registryName string = registry.name
output loginServer string = registry.properties.loginServer
output registryId string = registry.id
