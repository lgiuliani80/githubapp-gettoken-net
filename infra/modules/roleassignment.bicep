@description('Principal ID to assign the role to')
param principalId string

@description('Role definition ID to assign')
param roleDefinitionId string

@description('Resource ID of the resource to assign the role on')
param targetResourceId string

@description('Principal type of the assignee')
param principalType string = 'ServicePrincipal'

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(principalId, roleDefinitionId, targetResourceId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: principalType
  }
}
