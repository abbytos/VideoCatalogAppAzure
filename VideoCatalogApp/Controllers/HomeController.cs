using Microsoft.AspNetCore.Mvc;
using VideoCatalogApp.Models;
using Azure.Storage.Blobs;
using System.Diagnostics;
using Azure.Storage.Sas;
using Azure.Storage;
using Azure;
using System.Net.Http.Headers;
using VideoCatalogAppAzure.Utils;

namespace VideoCatalogApp.Controllers
{
    /// <summary>
    /// Controller for managing video-related actions including fetching, uploading, and generating SAS tokens for video files.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HomeController> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Constructor to initialize the HomeController with necessary services and configurations.
        /// </summary>
        /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
        /// <param name="logger">Logger for logging messages and errors.</param>
        /// <param name="blobServiceClient">Client to interact with Azure Blob Storage.</param>
        /// <param name="configuration">Configuration for accessing settings.</param>
        public HomeController(IHttpClientFactory httpClientFactory, ILogger<HomeController> logger, BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Action method to display the list of videos by fetching data from Azure Functions.
        /// </summary>
        /// <returns>View displaying the list of videos.</returns>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Retrieve Azure Functions base URL and API key from configuration
                var azureFunctionsBaseUrl = _configuration["AzureFunctionsBaseUrl"];
                var apiKey = _configuration["AzureFunctionApiKey"];

                // Create HTTP request to call Azure Functions API for listing videos
                var request = new HttpRequestMessage(HttpMethod.Get, $"{azureFunctionsBaseUrl}/api/ListVideos");
                request.Headers.Add("x-functions-key", apiKey); // Add API key to request header

                var response = await _httpClientFactory.CreateClient().SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Parse response content to list of video models
                var videos = await response.Content.ReadFromJsonAsync<List<VideoFileModel>>();
                if (videos == null)
                {
                    throw new Exception("Error fetching videos");
                }

                return View(videos); // Return view with video list
            }
            catch (Exception ex)
            {
                // Log error and return error view
                _logger.LogError(ex, "Error fetching videos in Index method.");
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        /// <summary>
        /// Action method to generate a SAS token and return a URI for playing a video.
        /// </summary>
        /// <param name="fileName">Name of the video file to play.</param>
        /// <returns>View displaying the video playback URI.</returns>
        public async Task<IActionResult> Play(string fileName)
        {
            try
            {
                // Get blob container and blob client
                var containerClient = _blobServiceClient.GetBlobContainerClient(_configuration["BlobContainerName"]);
                var blobClient = containerClient.GetBlobClient(fileName);

                // Create SAS token with read permissions
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _configuration["BlobContainerName"],
                    BlobName = fileName,
                    Resource = "b", // 'b' for blob
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) // Set expiry time for the SAS token
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var storageAccountName = _configuration["AzureStorageAccountName"];
                var storageAccountKey = _configuration["AzureStorageAccountKey"];
                var sasToken = sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(storageAccountName, storageAccountKey)).ToString();
                sasToken = System.Net.WebUtility.UrlEncode(sasToken); // URL encode the SAS token

                var blobUri = $"{blobClient.Uri}?{sasToken}"; // Create the URI with SAS token

                return View("Play", (object)blobUri); // Return view with video playback URI
            }
            catch (Exception ex)
            {
                // Log error and return error view
                _logger.LogError(ex, $"Error playing video: {fileName}");
                return View("Error");
            }
        }

        /// <summary>
        /// Action method to check if a specific blob exists in the specified container.
        /// </summary>
        /// <param name="containerName">Name of the blob container.</param>
        /// <param name="blobName">Name of the blob.</param>
        /// <returns>Status indicating whether the blob exists or not.</returns>
        public async Task<IActionResult> CheckBlobExists(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if the blob exists
                var exists = await blobClient.ExistsAsync().ConfigureAwait(false);

                if (exists)
                {
                    return Ok("Blob exists.");
                }
                else
                {
                    return NotFound("Blob not found.");
                }
            }
            catch (RequestFailedException ex)
            {
                // Log error and return status code
                _logger.LogError($"Error checking blob existence: {ex.Message}");
                return StatusCode((int)ex.Status, $"Request failed: {ex.ErrorCode}");
            }
        }

        /// <summary>
        /// Action method to handle file uploads and forward them to Azure Functions for processing.
        /// </summary>
        /// <param name="files">List of files to upload.</param>
        /// <returns>Status indicating the result of the upload operation.</returns>
        [HttpPost("api/upload")]
        public async Task<IActionResult> Upload(List<IFormFile> files)
        {
            _logger.LogInformation("Received {0} files for upload.", files?.Count ?? 0);

            if (files == null || files.Count == 0)
            {
                _logger.LogWarning("No files received for upload.");
                return BadRequest("No files received for upload.");
            }

            var invalidFiles = new List<string>();
            var successfulUploads = new List<string>();

            foreach (var file in files)
            {
                // Trim the file name to remove any extra quotes
                var trimmedFileName = file.FileName.Trim('"');

                // Check if the file is empty or does not have a .mp4 extension
                if (file.Length == 0 || !trimmedFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Invalid file received: {0}", trimmedFileName);
                    invalidFiles.Add(trimmedFileName);
                    continue;
                }

                try
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        var fileContent = new StreamContent(file.OpenReadStream());
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                        content.Add(fileContent, "file", trimmedFileName);

                        var azureFunctionsBaseUrl = _configuration["AzureFunctionsBaseUrl"];
                        var apiKey = _configuration["AzureFunctionApiKey"];

                        if (string.IsNullOrEmpty(azureFunctionsBaseUrl) || string.IsNullOrEmpty(apiKey))
                        {
                            _logger.LogError("Azure Functions Base URL or API Key is not configured.");
                            invalidFiles.Add(trimmedFileName);
                            continue;
                        }

                        var request = new HttpRequestMessage(HttpMethod.Post, $"{azureFunctionsBaseUrl}/api/UploadVideo")
                        {
                            Content = content
                        };
                        request.Headers.Add("x-functions-key", apiKey);

                        var response = await _httpClientFactory.CreateClient().SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        successfulUploads.Add(trimmedFileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading video: {0}", trimmedFileName);
                    invalidFiles.Add(trimmedFileName);
                }
            }

            if (invalidFiles.Count > 0)
            {
                _logger.LogWarning("Some files were invalid or failed to upload: {0}", string.Join(", ", invalidFiles));
                return BadRequest(new { message = "Some files were invalid or failed to upload.", invalidFiles });
            }

            _logger.LogInformation("Successfully uploaded files: {0}", string.Join(", ", successfulUploads));
            return Ok(new { message = "All files uploaded successfully.", successfulUploads });
        }


        /// <summary>
        /// Action method to generate a SAS token for accessing a specific blob.
        /// </summary>
        /// <param name="fileName">Name of the video file to generate a SAS token for.</param>
        /// <returns>JSON response containing the SAS token URI.</returns>
        [HttpGet("api/getSasToken")]
        public IActionResult GetSasToken(string fileName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_configuration["BlobContainerName"]);
                var blobClient = containerClient.GetBlobClient(fileName);

                // Create SAS token with read permissions
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _configuration["BlobContainerName"],
                    BlobName = fileName,
                    Resource = "b", // 'b' for blob
                    StartsOn = DateTimeOffset.UtcNow, // Start time
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(8) // Expiry time
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read); // Read permissions

                var storageAccountName = _configuration["StorageAccountName"];
                var storageAccountKey = _configuration["StorageAccountKey"];

                var sasToken = sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(storageAccountName, storageAccountKey)).ToString();
                var sasUri = $"{blobClient.Uri.AbsoluteUri}?{sasToken}"; // Create the SAS URI

                return Ok(new { sasUri }); // Return SAS URI
            }
            catch (Exception ex)
            {
                // Log error and return internal server error status
                _logger.LogError(ex, $"Error generating SAS token for file: {fileName}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Action method to fetch and return the list of videos from Azure Functions as JSON.
        /// </summary>
        /// <returns>JSON response containing the list of videos.</returns>
        [HttpGet("api/videos")]
        public async Task<IActionResult> GetVideos()
        {
            try
            {
                var azureFunctionsBaseUrl = _configuration["AzureFunctionsBaseUrl"];
                var apiKey = _configuration["AzureFunctionApiKey"];
                var request = new HttpRequestMessage(HttpMethod.Get, $"{azureFunctionsBaseUrl}/api/ListVideos");
                request.Headers.Add("x-functions-key", apiKey); // Add API key to request header

                var response = await _httpClientFactory.CreateClient().SendAsync(request);
                response.EnsureSuccessStatusCode();

                var videos = await response.Content.ReadFromJsonAsync<List<VideoFileModel>>();
                if (videos == null)
                {
                    throw new Exception("Error fetching videos");
                }

                return Json(videos); // Return JSON response with video list
            }
            catch (Exception ex)
            {
                // Log error and return internal server error status
                _logger.LogError(ex, "Error fetching videos in GetVideos method.");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
