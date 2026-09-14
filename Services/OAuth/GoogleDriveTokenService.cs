using BackupPro.Data;
using BackupPro.Models;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.OAuth
{
    /// <summary>
    /// Renueva y valida el access token de una configuración de Google Drive guardada. Se usa tanto
    /// desde <see cref="Controllers.StorageTypes.GoogleDriveStorageController"/> (para explorar
    /// carpetas sin exponer el token al navegador) como desde
    /// <see cref="Backup.GoogleDriveStorageProvider"/> (para subir un backup) — antes esta lógica
    /// estaba duplicada porque solo vivía en el controlador.
    /// </summary>
    public class GoogleDriveTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleOAuthSettings _googleOAuthSettings;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<GoogleDriveTokenService> _logger;

        public GoogleDriveTokenService(ApplicationDbContext context, GoogleOAuthSettings googleOAuthSettings, CredentialProtector credentialProtector, ILogger<GoogleDriveTokenService> logger)
        {
            _context = context;
            _googleOAuthSettings = googleOAuthSettings;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        /// <summary>Verifica si el token está expirado y lo renueva si es necesario.</summary>
        public async Task<bool> EnsureValidTokenAsync(GoogleDriveStorage googleDriveStorage)
        {
            if (string.IsNullOrEmpty(googleDriveStorage.AccessToken))
            {
                return false;
            }

            if (googleDriveStorage.TokenExpiresAt.HasValue && googleDriveStorage.TokenExpiresAt.Value > DateTime.Now.AddMinutes(5))
            {
                return true;
            }

            return await RefreshAccessTokenAsync(googleDriveStorage);
        }

        /// <summary>Devuelve el AccessToken ya descifrado y listo para usar (llamar después de <see cref="EnsureValidTokenAsync"/>).</summary>
        public string GetPlainAccessToken(GoogleDriveStorage googleDriveStorage) =>
            _credentialProtector.Unprotect(googleDriveStorage.AccessToken) ?? string.Empty;

        private async Task<bool> RefreshAccessTokenAsync(GoogleDriveStorage googleDriveStorage)
        {
            try
            {
                if (string.IsNullOrEmpty(googleDriveStorage.RefreshToken))
                {
                    return false;
                }

                string plainRefreshToken = _credentialProtector.Unprotect(googleDriveStorage.RefreshToken) ?? string.Empty;
                var tokenRequest = new Dictionary<string, string>
                {
                    { "refresh_token", plainRefreshToken },
                    { "client_id", _googleOAuthSettings.ClientId! },
                    { "client_secret", _googleOAuthSettings.ClientSecret! },
                    { "grant_type", "refresh_token" }
                };

                using var httpClient = new HttpClient();
                var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(tokenRequest));

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("No se pudo renovar el token de Google Drive: {Error}", errorContent);
                    return false;
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = System.Text.Json.JsonDocument.Parse(tokenJson);
                var newAccessToken = tokenData.RootElement.GetProperty("access_token").GetString();

                // Actualizar el token en la base de datos (cifrado). Google generalmente no devuelve
                // un nuevo refresh_token en la renovación; el original sigue siendo válido.
                googleDriveStorage.AccessToken = _credentialProtector.Protect(newAccessToken);
                googleDriveStorage.TokenExpiresAt = DateTime.Now.AddHours(1);
                googleDriveStorage.LastModifiedAt = DateTime.Now;

                _context.GoogleDriveStorages.Update(googleDriveStorage);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al renovar el token de Google Drive");
                return false;
            }
        }
    }
}
