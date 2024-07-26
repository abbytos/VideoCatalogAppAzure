using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace VideoCatalogAppAzure.Utils
{
    /// <summary>
    /// Helper class for interacting with Azure Key Vault to retrieve secrets.
    /// </summary>
    public class KeyVaultHelper
    {
        private static SecretClient _secretClient;

        /// <summary>
        /// Initializes the <see cref="KeyVaultHelper"/> with the specified Key Vault endpoint.
        /// This method should be called before using other methods of the class.
        /// </summary>
        /// <param name="endpoint">The URI of the Azure Key Vault.</param>
        public static void Initialize(string endpoint)
        {
            _secretClient = new SecretClient(new Uri(endpoint), new DefaultAzureCredential());
        }

        /// <summary>
        /// Retrieves the value of a secret from Azure Key Vault.
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve.</param>
        /// <returns>The value of the secret.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the KeyVaultHelper has not been initialized.</exception>
        /// <exception cref="RequestFailedException">Thrown if the request to Azure Key Vault fails.</exception>
        public static async Task<string> GetSecretAsync(string secretName)
        {
            try
            {
                if (_secretClient == null)
                {
                    throw new InvalidOperationException("KeyVaultHelper has not been initialized.");
                }

                KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
                return secret.Value;
            }
            catch (Exception ex)
            {
                // Rethrow the exception to be handled by the caller
                throw;
            }
        }
    }
}
