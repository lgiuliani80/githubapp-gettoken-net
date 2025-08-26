@description('Name of the App Service')
param name string

@description('Location to deploy the App Service')
param location string

@description('App Service Plan ID')
param appServicePlanId string

@description('Tags to apply to the App Service')
param tags object = {}

@description('Key Vault name')
param keyVaultName string

@description('GitHub App Client ID')
param githubAppClientId string

@description('Name of the secret where GitHub private key will be stored')
param githubPrivateKeySecretName string

@description('Entra ID tenant ID')
param entraTenantId string

@description('Entra ID application client ID')
param entraClientId string

@description('Valid audincese for the tokens')
param validAudiences string

@description('Entra ID domain')
param entraDomain string

@description('Whether authentication is required')
param requireAuthentication bool = true

@description('Whether to map OpenAPI documentation')
param mapOpenApi bool = false

var validAudiencesArray = empty(validAudiences) ? [] : split(validAudiences, ',')
var extraAudiences = [for (audience, i) in validAudiencesArray: {
  name: 'AzureAd__TokenValidationParameters__ValidAudiences__${i + 1}'
  value: audience
}]
var clientIdAudience = [{
  name: 'AzureAd__TokenValidationParameters__ValidAudiences__0'
  value: entraClientId
}]

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: concat([
        {
          name: 'AzureAd__TenantId'
          value: entraTenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: entraClientId
        }
        {
          name: 'AzureAd__Domain'
          value: entraDomain
        }
        {
          name: 'RequireAuthentication'
          value: string(requireAuthentication)
        }
        {
          name: 'MapOpenApi'
          value: string(mapOpenApi)
        }
        {
          name: 'Github__ClientId'
          value: githubAppClientId
        }
        {
          name: 'Github__PrivateKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=${githubPrivateKeySecretName})'
        }
      ], clientIdAudience, extraAudiences)
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

output uri string = 'https://${webApp.properties.defaultHostName}'
output name string = webApp.name
output identityPrincipalId string = webApp.identity.principalId
