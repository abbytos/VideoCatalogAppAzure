# Variables
$resourceGroup = "VideoCatalogAppResourceGroup"
$bicepFile = "keys.bicep"
$location = "Australia East" 
$storageAccountName = "videocatalogstorageacct"
$keyVaultName = "VideoCatalogAppKeyVault"
$blobContainerName = "videos"
$webAppName = "videocatalogwebapp"
$functionAppName = "videocatalogfunctionapp"
$appInsightsName = "videocatalogappinsights"

# Retrieve the object ID of the signed-in user
try {
    $objectId = "29d2aceb-d693-408a-877a-47833b678b3b" #az ad signed-in-user show --query id --output tsv
    Write-Output "Retrieved Object ID: $objectId"

    if (-not $objectId) {
        throw "Failed to retrieve the object ID of the signed-in user."
    }

    # Deploy the Bicep template with the retrieved object ID
    $deployment = az deployment group create `
        --resource-group $resourceGroup `
        --template-file $bicepFile `
        --parameters location=$location `
                     storageAccountName=$storageAccountName `
                     keyVaultName=$keyVaultName `
                     blobContainerName=$blobContainerName `
                     webAppName=$webAppName `
                     functionAppName=$functionAppName `
                     appInsightsName=$appInsightsName `
                     objectId=$objectId

    if ($LASTEXITCODE -ne 0) {
        throw "Deployment failed."
    } else {
        Write-Output "Deployment succeeded."
    }
} catch {
    Write-Error "An error occurred: $_.Exception.Message"
    exit 1
}
