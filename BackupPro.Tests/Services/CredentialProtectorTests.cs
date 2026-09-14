using BackupPro.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupPro.Tests.Services
{
    public class CredentialProtectorTests
    {
        private static CredentialProtector CreateProtector()
        {
            // Clave maestra fija de 32 bytes (AES-256) para que las pruebas sean deterministas y no
            // dependan de un archivo Data/master.key generado en disco.
            var masterKey = Convert.ToBase64String(new byte[32]);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:MasterKey"] = masterKey
                })
                .Build();

            return new CredentialProtector(configuration, NullLogger<CredentialProtector>.Instance);
        }

        [Fact]
        public void Protect_ThenUnprotect_RoundTripsOriginalValue()
        {
            var protector = CreateProtector();
            const string original = "SuperSecreta123!";

            var protectedValue = protector.Protect(original);
            var unprotected = protector.Unprotect(protectedValue);

            Assert.Equal(original, unprotected);
        }

        [Fact]
        public void Protect_SameValueTwice_ProducesDifferentCiphertext()
        {
            var protector = CreateProtector();

            var first = protector.Protect("mismo-valor");
            var second = protector.Protect("mismo-valor");

            Assert.NotEqual(first, second);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Protect_NullOrEmpty_ReturnsSameValue(string? value)
        {
            var protector = CreateProtector();

            Assert.Equal(value, protector.Protect(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Unprotect_NullOrEmpty_ReturnsSameValue(string? value)
        {
            var protector = CreateProtector();

            Assert.Equal(value, protector.Unprotect(value));
        }

        [Fact]
        public void Unprotect_LegacyPlainTextValue_ReturnsUnchanged()
        {
            var protector = CreateProtector();
            const string legacyPlainText = "contraseña-guardada-antes-del-cifrado";

            var result = protector.Unprotect(legacyPlainText);

            Assert.Equal(legacyPlainText, result);
        }

        [Fact]
        public void IsProtected_ProtectedValue_ReturnsTrue()
        {
            var protector = CreateProtector();

            var protectedValue = protector.Protect("valor");

            Assert.True(protector.IsProtected(protectedValue));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("texto-plano-sin-cifrar")]
        public void IsProtected_UnprotectedValue_ReturnsFalse(string? value)
        {
            var protector = CreateProtector();

            Assert.False(protector.IsProtected(value));
        }
    }
}
