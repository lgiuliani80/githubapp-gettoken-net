@description('Name for the App Service Plan')
param name string

@description('Location to deploy the App Service Plan')
param location string

@description('App Service Plan pricing tier')
param sku object

@description('App Service Plan OS type')
param kind string = 'linux'

@description('Tags to apply to App Service Plan')
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: name
  location: location
  tags: tags
  sku: sku
  kind: kind
  properties: {
    reserved: kind == 'linux'
  }
}

output id string = appServicePlan.id
output name string = appServicePlan.name