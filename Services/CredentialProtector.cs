using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace BackupPro.Services
{
    /// <summary>
    /// Cifra y descifra de forma reversible las credenciales sensibles que BackupPro necesita
    /// reutilizar (contraseñas de bases de datos, cadenas de conexión, tokens OAuth) antes de
    /// guardarlas en la base de datos.
    ///
    /// La clave de cifrado se deriva con Argon2id (memory-hard, resistente a fuerza bruta) a partir
    /// de una clave maestra, y el cifrado en sí se hace con AES-256-GCM (autenticado). Cada valor
    /// usa una sal y un nonce distintos, así que el mismo texto plano nunca produce el mismo
    /// resultado dos veces.
    /// </summary>
    public class CredentialProtector
    {
        private const string Prefix = "ARGON2GCM:";
        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32; // AES-256

        private readonly byte[] _masterKey;

        public CredentialProtector(IConfiguration configuration, ILogger<CredentialProtector> logger)
        {
            _masterKey = ResolveMasterKey(configuration, logger);
        }

        /// <summary>
        /// Cifra un texto plano. Los valores null o vacíos se devuelven tal cual (nada que cifrar).
        /// </summary>
        public string? Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = DeriveKey(salt);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSize];

            using (var aesGcm = new AesGcm(key, TagSize))
            {
                aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            // Formato empaquetado: salt || nonce || tag || ciphertext
            byte[] packed = new byte[SaltSize + NonceSize + TagSize + cipherBytes.Length];
            Buffer.BlockCopy(salt, 0, packed, 0, SaltSize);
            Buffer.BlockCopy(nonce, 0, packed, SaltSize, NonceSize);
            Buffer.BlockCopy(tag, 0, packed, SaltSize + NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, packed, SaltSize + NonceSize + TagSize, cipherBytes.Length);

            return Prefix + Convert.ToBase64String(packed);
        }

        /// <summary>
        /// Descifra un valor generado por <see cref="Protect"/>. Si el valor no lleva el prefijo
        /// esperado se asume que es un dato legado guardado en texto plano por una versión anterior
        /// y se devuelve sin modificar (se recifrará la próxima vez que se guarde).
        /// </summary>
        public string? Unprotect(string? protectedText)
        {
            if (string.IsNullOrEmpty(protectedText) || !protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return protectedText;
            }

            byte[] packed = Convert.FromBase64String(protectedText.Substring(Prefix.Length));

            byte[] salt = packed[..SaltSize];
            byte[] nonce = packed[SaltSize..(SaltSize + NonceSize)];
            byte[] tag = packed[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
            byte[] cipherBytes = packed[(SaltSize + NonceSize + TagSize)..];

            byte[] key = DeriveKey(salt);
            byte[] plainBytes = new byte[cipherBytes.Length];

            using (var aesGcm = new AesGcm(key, TagSize))
            {
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Indica si un valor ya está cifrado con este protector (útil para no volver a cifrar dos veces).
        /// </summary>
        public bool IsProtected(string? value) => !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

        private byte[] DeriveKey(byte[] salt)
        {
            using var argon2 = new Argon2id(_masterKey)
            {
                Salt = salt,
                DegreeOfParallelism = 1,
                Iterations = 2,
                MemorySize = 19 * 1024 // 19 MiB, perfil recomendado por OWASP para Argon2id
            };

            return argon2.GetBytes(KeySize);
        }

        private static byte[] ResolveMasterKey(IConfiguration configuration, ILogger logger)
        {
            var configured = Environment.GetEnvironmentVariable("BACKUPPRO_MASTER_KEY");
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = configuration["Security:MasterKey"];
            }

            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Convert.FromBase64String(configured);
            }

            // No hay clave configurada. Generamos una y la persistimos fuera del control de código
            // fuente para que la app siga funcionando de inmediato. En producción se recomienda
            // definir la variable de entorno BACKUPPRO_MASTER_KEY (ver README) en vez de depender
            // de este archivo generado automáticamente.
            // Se usa AppContext.BaseDirectory (no IHostEnvironment.ContentRootPath) para que la
            // clave quede junto a backuppro.db (ver Program.cs), en la carpeta de salida del build,
            // y no en el árbol de código fuente al ejecutar con "dotnet run".
            string keyFolder = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(keyFolder);
            string keyFile = Path.Combine(keyFolder, "master.key");

            if (File.Exists(keyFile))
            {
                return Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
            }

            byte[] newKey = RandomNumberGenerator.GetBytes(KeySize);
            File.WriteAllText(keyFile, Convert.ToBase64String(newKey));

            logger.LogWarning(
                "No se configuró BACKUPPRO_MASTER_KEY. Se generó automáticamente una clave maestra en {KeyFile}. " +
                "Para producción, define la variable de entorno BACKUPPRO_MASTER_KEY (ver README) y elimina este archivo.",
                keyFile);

            return newKey;
        }
    }
}
