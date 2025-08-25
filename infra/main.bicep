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

@description('GitHub App Client ID')
param githubAppClientId string = ''

@description('Name of the secret where GitHub private key will be stored')
param githubPrivateKeySecretName string = 'github-private-key'

@description('Entra ID tenant ID')
param entraTenantId string = subscription().tenantId

@description('Entra ID application client ID')
param entraClientId string

@description('Valid audincese for the tokens')
param validAudiences string = ''

@description('Entra ID domain')
param entraDomain string = subscription().tenantId

@description('Whether authentication is required')
param requireAuthentication bool = true

@description('Whether to map OpenAPI documentation')
param mapOpenApi bool = false

var tags = {
  'azd-env-name': environmentName
}

var effectiveClientId = empty(entraClientId) ? '00000000-0000-0000-0000-000000000000' : entraClientId
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
    entraClientId: effectiveClientId
    entraDomain: effectiveDomain
    validAudiences: validAudiences
    requireAuthentication: requireAuthentication
    mapOpenApi: mapOpenApi
  }
}

// App outputs
output WEB_URI string = appModule.outputs.webUri
output AZURE_LOCATION string = location
output AZURE_KEY_VAULT_NAME string = appModule.outputs.keyVaultName
output AZURE_APP_SERVICE_NAME string = appModule.outputs.appServiceName
