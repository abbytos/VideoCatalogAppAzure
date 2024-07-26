namespace VideoCatalogAppAzure.Utils
{
    public class AzureBlobSettings
    {
        public string BlobContainerName { get; set; }
        public string AzureFunctionsBaseUrl { get; set; }
        public string AzureFunctionApiKey { get; set; }
        public string StorageAccountName { get; set; }
        public string StorageAccountKey { get; set; }
    }
}
