param location string = resourceGroup().location
param storageAccountName string = 'videocatalogstorageacct'
param keyVaultName string
param blobContainerName string = 'videos'
param webAppName string = 'videocatalogwebapp'
param functionAppName string = 'videocatalogfunctionapp'
param appInsightsName string = 'videocatalogappinsights'
param objectId string // Object ID of the user, group, or service principal

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
}

// Blob Services
resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  name: 'default'
  parent: storageAccount
}

// Blob Container
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: blobContainerName
  parent: blobServices
  properties: {
    publicAccess: 'None' // Ensure public access is disabled
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: objectId
        permissions: {
          secrets: [
            'get'
            'list'
            'set'
          ]
        }
      }
    ]
  }
}

// Secrets
resource storageAccountKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'StorageAccountKey'
  properties: {
    value: listKeys(storageAccount.id, '2022-09-01').keys[0].value
  }
}

resource blobContainerNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'BlobContainerName'
  properties: {
    value: blobContainerName
  }
}

resource webAppNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'WebAppName'
  properties: {
    value: webAppName
  }
}

resource functionAppNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'FunctionAppName'
  properties: {
    value: functionAppName
  }
}

resource appInsightsNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AppInsightsName'
  properties: {
    value: appInsightsName
  }
}

// Outputs
output keyVaultUri string = keyVault.properties.vaultUri
output storageAccountKeyUri string = storageAccountKeySecret.properties.secretUriWithVersion
output blobContainerNameUri string = blobContainerNameSecret.properties.secretUriWithVersion
output webAppNameUri string = webAppNameSecret.properties.secretUriWithVersion
output functionAppNameUri string = functionAppNameSecret.properties.secretUriWithVersion
output appInsightsNameUri string = appInsightsNameSecret.properties.secretUriWithVersion
