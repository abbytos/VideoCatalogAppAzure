# Variables
$resourceGroup = "VideoCatalogAppResourceGroup"
$location = "Australia East"
$storageAccountName = "videocatalogstorageacct"
$functionAppName = "videocatalogfunctionapp"
$webAppName = "videocatalogwebapp"
$appServicePlanName = "VideoCatalogAppServicePlan"
$blobContainerName = "videos"
$allowedOrigins = "*"
$appInsightsName = "videocatalogappinsights"
$keyVaultName = "VideoCatalogAppKeyVault"

# Deploy the main Bicep template
az deployment group create --resource-group $resourceGroup --template-file ./main.bicep `
  --parameters location=$location `
                storageAccountName=$storageAccountName `
                functionAppName=$functionAppName `
                appServicePlanName=$appServicePlanName `
                webAppName=$webAppName `
                blobContainerName=$blobContainerName `
                allowedOrigins=$allowedOrigins `
                appInsightsName=$appInsightsName `
                keyVaultName=$keyVaultName

# Enable System-Assigned Managed Identity for Function App
az functionapp identity assign --name $functionAppName --resource-group $resourceGroup
az webapp identity assign --name $webAppName --resource-group $resourceGroup

# Fetch Principal IDs
$functionAppPrincipalId = (az functionapp show --name $functionAppName --resource-group $resourceGroup --query identity.principalId --output tsv)
$webAppPrincipalId = (az webapp show --name $webAppName --resource-group $resourceGroup --query identity.principalId --output tsv)

# Print Principal IDs
Write-Output "Function App Principal ID: $functionAppPrincipalId"
Write-Output "Web App Principal ID: $webAppPrincipalId"

# Deploy the Key Vault update Bicep template with retrieved principal IDs
az deployment group create --resource-group $resourceGroup --template-file ./update-keyvault.bicep `
  --parameters location=$location `
                keyVaultName=$keyVaultName `
                functionAppPrincipalId=$functionAppPrincipalId `
                webAppPrincipalId=$webAppPrincipalId
