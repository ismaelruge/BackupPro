using BackupPro.Data;
using BackupPro.Models;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.OAuth
{
    /// <summary>
    /// Renueva y valida el access token de una configuración de OneDrive guardada. Se usa tanto
    /// desde <see cref="Controllers.StorageTypes.OneDriveStorageController"/> (para explorar
    /// carpetas sin exponer el token al navegador) como desde
    /// <see cref="Backup.OneDriveStorageProvider"/> (para subir un backup) — antes esta lógica
    /// estaba duplicada porque solo vivía en el controlador.
    /// </summary>
    public class OneDriveTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly OneDriveSettings _oneDriveSettings;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<OneDriveTokenService> _logger;

        public OneDriveTokenService(ApplicationDbContext context, OneDriveSettings oneDriveSettings, CredentialProtector credentialProtector, ILogger<OneDriveTokenService> logger)
        {
            _context = context;
            _oneDriveSettings = oneDriveSettings;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        /// <summary>Verifica si el token está expirado y lo renueva si es necesario.</summary>
        public async Task<bool> EnsureValidTokenAsync(OneDriveStorage oneDriveStorage)
        {
            if (string.IsNullOrEmpty(oneDriveStorage.AccessToken))
            {
                return false;
            }

            if (oneDriveStorage.TokenExpiresAt.HasValue && oneDriveStorage.TokenExpiresAt.Value > DateTime.Now.AddMinutes(5))
            {
                return true;
            }

            return await RefreshAccessTokenAsync(oneDriveStorage);
        }

        /// <summary>Devuelve el AccessToken ya descifrado y listo para usar (llamar después de <see cref="EnsureValidTokenAsync"/>).</summary>
        public string GetPlainAccessToken(OneDriveStorage oneDriveStorage) =>
            _credentialProtector.Unprotect(oneDriveStorage.AccessToken) ?? string.Empty;

        private async Task<bool> RefreshAccessTokenAsync(OneDriveStorage oneDriveStorage)
        {
            try
            {
                if (string.IsNullOrEmpty(oneDriveStorage.RefreshToken))
                {
                    return false;
                }

                string plainRefreshToken = _credentialProtector.Unprotect(oneDriveStorage.RefreshToken) ?? string.Empty;
                var tokenRequest = new Dictionary<string, string>
                {
                    { "refresh_token", plainRefreshToken },
                    { "client_id", _oneDriveSettings.ClientId! },
                    { "client_secret", _oneDriveSettings.ClientSecret! },
                    { "grant_type", "refresh_token" }
                };

                using var httpClient = new HttpClient();
                var tokenResponse = await httpClient.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token",
                    new FormUrlEncodedContent(tokenRequest));

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = System.Text.Json.JsonDocument.Parse(tokenJson);
                var newAccessToken = tokenData.RootElement.GetProperty("access_token").GetString();

                // Actualizar el token en la base de datos (cifrado)
                oneDriveStorage.AccessToken = _credentialProtector.Protect(newAccessToken);

                if (tokenData.RootElement.TryGetProperty("refresh_token", out var newRefreshToken))
                {
                    oneDriveStorage.RefreshToken = _credentialProtector.Protect(newRefreshToken.GetString());
                }

                oneDriveStorage.TokenExpiresAt = DateTime.Now.AddHours(1);
                oneDriveStorage.LastModifiedAt = DateTime.Now;

                _context.OneDriveStorages.Update(oneDriveStorage);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al renovar el token de OneDrive");
                return false;
            }
        }
    }
}
