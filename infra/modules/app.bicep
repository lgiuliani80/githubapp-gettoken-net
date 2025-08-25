// App module will be populated

@description('Name of the environment')
param environmentName string

@description('Azure region used for deployment')
param location string

@description('App Service Plan resource ID')
param appServicePlanSkuName string = 'B1' // B1 is the Basic tier, 1 vCPU, 1.75 GB RAM

@description('Tags that should be applied to created resources')
param tags object = {}

@description('Id of the principal to assign role to')
param principalId string = ''

@description('Principal type: User, Group, ServicePrincipal, ForeignGroup')
param principalType string = 'User'

@description('GitHub App Client ID')
param githubAppClientId string = ''

@description('Name of the secret where GitHub private key will be stored')
param githubPrivateKeySecretName string = 'github-private-key'

@description('GitHub private key in PEM format')
@secure()
param githubPrivateKey string

@description('Entra ID tenant ID')
param entraTenantId string

@description('Entra ID application client ID')
param entraClientId string

@description('Entra ID domain')
param entraDomain string

@description('Valid audincese for the tokens')
param validAudiences string

@description('Whether authentication is required')
param requireAuthentication bool = true

@description('Whether to map OpenAPI documentation')
param mapOpenApi bool = false

var abbrs = loadJsonContent('../abbreviations.json')
var appname = 'ghau'

// Generate a unique token to be used in naming resources
// var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Create an App Service Plan to group applications under the same payment plan and SKU
module appServicePlan 'appserviceplan.bicep' = {
  name: 'appserviceplan'
  params: {
    name: '${abbrs.webServerFarms}-${appname}-${environmentName}'
    location: location
    tags: tags
    sku: {
      name: appServicePlanSkuName
      capacity: 1
    }
    kind: 'linux'
  }
}

// Create a Key Vault for storing secrets
module keyVault 'keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: '${abbrs.keyVaultVaults}-${appname}-${environmentName}'
    location: location
    tags: tags
    githubPrivateKey: githubPrivateKey
  }
}

// Create the App Service Web App
module app 'appservice.bicep' = {
  name: 'appservice'
  params: {
    name: '${abbrs.webSitesAppService}-${appname}-${environmentName}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.outputs.id
    keyVaultName: keyVault.outputs.name
    githubAppClientId: githubAppClientId
    githubPrivateKeySecretName: githubPrivateKeySecretName
    entraTenantId: entraTenantId
    entraClientId: entraClientId
    entraDomain: entraDomain
    validAudiences: validAudiences
    requireAuthentication: requireAuthentication
    mapOpenApi: mapOpenApi
  }
}

// Assign the "Key Vault Secrets User" role to the App Service
module roleAssignmentKvAppSecretUser 'roleassignment.bicep' = {
  name: 'role-assignment-kv-app-secretuser'
  params: {
    principalId: app.outputs.identityPrincipalId
    roleDefinitionId: '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
    principalType: 'ServicePrincipal'
    targetResourceId: keyVault.outputs.id
  }
}

// Assign the "Key Vault Administrator" role to the App Service
module roleAssignmentKvCurrentUserKvAdmin 'roleassignment.bicep' = {
  name: 'role-assignment-kv-currentuser-kvadmin'
  params: {
    principalId: principalId
    roleDefinitionId: '00482a5a-887f-4fb3-b363-3b7fe8e74483' // Key Vault Administrator
    principalType: principalType
    targetResourceId: keyVault.outputs.id
  }
}

// App outputs
output webUri string = app.outputs.uri
output keyVaultName string = keyVault.outputs.name
output appServiceName string = app.outputs.name
