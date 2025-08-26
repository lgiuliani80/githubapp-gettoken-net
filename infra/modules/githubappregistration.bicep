extension microsoftGraphV1

@description('GitHub App Client ID')
param githubAppClientId string

@description('GitHub App Name')
param githubAppName string = ''

@description('Entra ID domain')
param entraDomain string

@description('Managed Identity name to assign GithubRunner role')
param runnerManagedIdentityNames string

@description('Managed Identity oid to assign GithubRunner role')
param runnerManagedIdentityObjectIds string

var miNames = empty(runnerManagedIdentityNames) ? [] : split(runnerManagedIdentityNames, ',')
var miOids = empty(runnerManagedIdentityObjectIds) ? [] : split(runnerManagedIdentityObjectIds, ',')                             

var identityUris = empty(entraDomain) ? [] : [
  'api://${entraDomain}/GithubApp/${githubAppName}'
]
var githubRunnerRoleId = guid(githubAppClientId, 'GithubRunner')

// App Registration
resource githubApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'Github-${githubAppName}'
  displayName: 'Github-${githubAppName}'
  signInAudience: 'AzureADMyOrg'
  identifierUris: identityUris
  appRoles: [
    {
      id: githubRunnerRoleId
      displayName: 'GithubRunner'
      description: 'Role for Github Runner access.'
      value: 'GithubRunner'
      allowedMemberTypes: [ 'Application' ]
    }
  ]
}

// Enterprise Application (Service Principal)
resource githubAppSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: githubApp.appId
  displayName: githubApp.displayName
  appRoleAssignmentRequired: true
}

// Give the Managed Identity access to the App Role
resource miToSp 'Microsoft.Graph/appRoleAssignedTo@v1.0' = [for (name, index) in miNames: {
  appRoleId: githubRunnerRoleId
  principalId: miOids[index]
  resourceDisplayName: name
  resourceId: githubAppSp.id
}]

output applicationIdUri string = empty(githubApp.identifierUris) ? githubApp.appId : githubApp.identifierUris[0]
output audience string = empty(githubApp.identifierUris) ? '' : githubApp.identifierUris[0]
output appId string = githubApp.appId
