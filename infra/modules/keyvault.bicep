@description('Name of the Key Vault')
param name string

@description('Location to deploy the Key Vault')
param location string

@description('Tags to apply to the Key Vault')
param tags object = {}

@description('Name of the secret where GitHub private key will be stored')
param githubPrivateKeySecretName string = 'github-private-key'

@description('GitHub private key in PEM format')
@secure()
param githubPrivateKey string

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

resource githubPrivateKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: githubPrivateKeySecretName
  properties: {
    value: githubPrivateKey
  }
}

output name string = keyVault.name
output id string = keyVault.id
output uri string = keyVault.properties.vaultUri
