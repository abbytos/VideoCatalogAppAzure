using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoCatalogApp.Controllers;
using VideoCatalogAppAzure.Utils;

namespace VideoCatalogApp
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Configure Azure Key Vault
            var keyVaultEndpoint = new Uri(Configuration["KeyVault:Endpoint"]);
            var keyVaultCredential = new DefaultAzureCredential();

            // Add Azure Key Vault configuration provider
            services.AddSingleton<IConfiguration>(provider =>
            {
                var configBuilder = new ConfigurationBuilder()
                    .AddConfiguration(Configuration)
                    .AddAzureKeyVault(keyVaultEndpoint, keyVaultCredential);

                return configBuilder.Build();
            });

            // Initialize Key Vault Helper
            KeyVaultHelper.Initialize(keyVaultEndpoint.AbsoluteUri);

            // Register BlobServiceClient with the connection string from Key Vault
            var connectionString = KeyVaultHelper.GetSecretAsync(Configuration["BlobStorage:KVConnectionStringSecretName"]).GetAwaiter().GetResult();
            services.AddSingleton(new BlobServiceClient(connectionString));

            // Configure HttpClient for Blob operations
            services.AddHttpClient("BlobClient", client =>
            {
                client.BaseAddress = new Uri(Configuration["BlobStorage:Endpoint"]);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            // Register HttpClient for HomeController
            services.AddHttpClient<HomeController>();

            // Add MVC services
            services.AddControllersWithViews();

            // Configure CORS policy
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .WithOrigins("https://videocatalogwebapp.azurewebsites.net", "https://localhost:7013")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
