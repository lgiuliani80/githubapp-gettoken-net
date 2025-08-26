extension microsoftGraphV1
targetScope = 'subscription'

@minLength(1)
@maxLength(16)
@description('Name of the environment that can be used as part of naming resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('App Service Plan resource ID')
param appServicePlanSkuName string

@description('Id of the principal to assign role to')
param principalId string = ''

@description('Principal type: User, Group, ServicePrincipal, ForeignGroup')
param principalType string = 'User'

@description('GitHub App Name')
param githubAppName string = ''

@description('GitHub App Client ID')
param githubAppClientId string = ''

@description('Name of the secret where GitHub private key will be stored')
param githubPrivateKeySecretName string = 'github-private-key'

@description('Entra ID tenant ID')
param entraTenantId string = subscription().tenantId

@description('Entra ID domain')
param entraDomain string = subscription().tenantId

@description('Whether authentication is required')
param requireAuthentication bool = true

@description('Whether to map OpenAPI documentation')
param mapOpenApi bool = false

@description('Managed Identities names to assign GithubRunner role (comma separated)')
param runnerManagedIdentityNames string = ''

@description('Managed Identity oids to assign GithubRunner role (comma separated)')
param runnerManagedIdentityIds string = ''

var tags = {
  'azd-env-name': environmentName
}

var effectiveDomain = empty(entraDomain) ? '${subscription().tenantId}.onmicrosoft.com' : entraDomain

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-GithubGetToken-${environmentName}'
  location: location
  tags: tags
}

// Core application module
module appModule 'modules/app.bicep' = {
  name: 'appModule'
  scope: rg
  params: {
    environmentName: environmentName
    location: location
    appServicePlanSkuName: appServicePlanSkuName
    tags: tags
    principalId: principalId
    principalType: principalType
    githubAppClientId: githubAppClientId
    githubPrivateKey: loadTextContent('../.azure/github-private-key.pem')
    githubPrivateKeySecretName: githubPrivateKeySecretName
    entraTenantId: entraTenantId
    entraClientId: githubAppRegistration.outputs.appId
    entraDomain: effectiveDomain
    validAudiences: githubAppRegistration.outputs.audience
    requireAuthentication: requireAuthentication
    mapOpenApi: mapOpenApi
  }
}


// App Registration, Enterprise Application, Role Assignment
module githubAppRegistration 'modules/githubappregistration.bicep' = {
  name: 'githubAppRegistration'
  scope: rg
  params: {
    githubAppClientId: githubAppClientId
    githubAppName: githubAppName
    entraDomain: entraDomain
    runnerManagedIdentityNames: runnerManagedIdentityNames
    runnerManagedIdentityObjectIds: runnerManagedIdentityIds
  }
}


// App outputs
output WEB_URI string = appModule.outputs.webUri
output AZURE_LOCATION string = location
output AZURE_KEY_VAULT_NAME string = appModule.outputs.keyVaultName
output AZURE_APP_SERVICE_NAME string = appModule.outputs.appServiceName
output APP_REGISTRATION_APPLICATION_ID_URI string = githubAppRegistration.outputs.applicationIdUri
