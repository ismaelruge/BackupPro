//using Azure.Identity;
//using Azure.Storage.Blobs;
//using BackupPro.Models;
//using Google.Apis.Auth.OAuth2;
//using Google.Apis.Drive.v3;
//using Google.Apis.Services;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Graph;
//using Microsoft.Graph.Models;
//using System.Net;

//namespace BackupPro.Services
//{
//    /// <summary>
//    /// Servicio de subida de archivos de backup a distintos destinos (FTP, Google Drive, OneDrive, Azure Blob).
//    /// </summary>
//    public class StorageService : Controller
//    {
//        private readonly IConfiguration _configuration;

//        public StorageService(IConfiguration configuration)
//        {
//            _configuration = configuration;
//        }

//        /// <summary>
//        /// Sube archivo a un servidor FTP
//        /// </summary>
//        public async Task UploadToFtpAsync(string ftpUrl, string user, string password, string localFile)
//        {
//            string remoteFile = $"{ftpUrl}/{Path.GetFileName(localFile)}";
//            #pragma warning disable SYSLIB0014
//            var request = (FtpWebRequest)WebRequest.Create(remoteFile);
//            #pragma warning restore SYSLIB0014
//            request.Method = WebRequestMethods.Ftp.UploadFile;
//            request.Credentials = new NetworkCredential(user, password);

//            byte[] fileContents = await System.IO.File.ReadAllBytesAsync(localFile);
//            request.ContentLength = fileContents.Length;

//            using (Stream requestStream = request.GetRequestStream())
//            {
//                await requestStream.WriteAsync(fileContents, 0, fileContents.Length);
//            }

//            using var response = (FtpWebResponse)await request.GetResponseAsync();
//            Console.WriteLine($"FTP Upload Complete: {response.StatusDescription}");
//        }

//        /// <summary>
//        /// Sube archivo a Google Drive usando refresh token para obtener access token.
//        /// </summary>
//        public async Task UploadToGoogleDriveAsync(CompanyConfig config, string localFile)
//        {
//            if (string.IsNullOrWhiteSpace(config.GoogleDriveRefreshToken) ||
//                string.IsNullOrWhiteSpace(config.GoogleDriveClientId) ||
//                string.IsNullOrWhiteSpace(config.GoogleDriveClientSecret))
//            {
//                throw new InvalidOperationException("Google Drive no está configurado correctamente.");
//            }

//            using var http = new HttpClient();
//            var form = new FormUrlEncodedContent(new Dictionary<string, string>
//            {
//                ["client_id"] = config.GoogleDriveClientId,
//                ["client_secret"] = config.GoogleDriveClientSecret,
//                ["refresh_token"] = config.GoogleDriveRefreshToken,
//                ["grant_type"] = "refresh_token"
//            });
//            var tokenResp = await http.PostAsync("https://oauth2.googleapis.com/token", form);
//            tokenResp.EnsureSuccessStatusCode();
//            var json = System.Text.Json.JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync());
//            var accessToken = json.RootElement.GetProperty("access_token").GetString();

//            var credential = GoogleCredential.FromAccessToken(accessToken);
//            var service = new DriveService(new BaseClientService.Initializer
//            {
//                HttpClientInitializer = credential,
//                ApplicationName = "BackupPro"
//            });

//            var fileMetadata = new Google.Apis.Drive.v3.Data.File
//            {
//                Name = Path.GetFileName(localFile)
//            };

//            using var stream = new FileStream(localFile, FileMode.Open, FileAccess.Read);
//            var request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
//            request.Fields = "id";
//            await request.UploadAsync();
//        }

//        /// <summary>
//        /// Sube archivo a OneDrive usando aplicación (app-only). Requiere TenantId y DriveId.
//        /// </summary>
//        public async Task UploadToOneDriveAsync(CompanyConfig config, string localFile)
//        {
//            var tenantId = Environment.GetEnvironmentVariable("ONEDRIVE_TENANT_ID") ?? _configuration["OneDrive:TenantId"];
//            var driveId = Environment.GetEnvironmentVariable("ONEDRIVE_DRIVE_ID") ?? _configuration["OneDrive:DriveId"];
//            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(config.OneDriveClientId) || string.IsNullOrWhiteSpace(config.OneDriveClientSecret) || string.IsNullOrWhiteSpace(driveId))
//            {
//                throw new InvalidOperationException("OneDrive no está configurado: requiere TenantId, ClientId, ClientSecret y DriveId.");
//            }

//            var scopes = new[] { "https://graph.microsoft.com/.default" };
//            var credential = new ClientSecretCredential(tenantId, config.OneDriveClientId, config.OneDriveClientSecret);
//            var graphClient = new GraphServiceClient(credential, scopes);

//            using var stream = new FileStream(localFile, FileMode.Open, FileAccess.Read);
//            var fileName = Path.GetFileName(localFile);
//            await graphClient.Drives[driveId].Root.ItemWithPath(fileName).Content.PutAsync(stream);
//        }

//        /// <summary>
//        /// Sube archivo a Azure Blob Storage
//        /// </summary>
//        public async Task UploadToAzureBlobAsync(CompanyConfig config, string localFile)
//        {
//            if (string.IsNullOrWhiteSpace(config.BlobConnectionString) || string.IsNullOrWhiteSpace(config.BlobContainerName))
//            {
//                throw new InvalidOperationException("Azure Blob Storage no está configurado.");
//            }

//            var blobServiceClient = new BlobServiceClient(config.BlobConnectionString);
//            var containerClient = blobServiceClient.GetBlobContainerClient(config.BlobContainerName);

//            await containerClient.CreateIfNotExistsAsync();
//            var blobClient = containerClient.GetBlobClient(Path.GetFileName(localFile));

//            using var stream = System.IO.File.OpenRead(localFile);
//            await blobClient.UploadAsync(stream, true);
//        }
//    }
//}
