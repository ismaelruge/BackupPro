using BackupPro.Binders;
using Xunit;

namespace BackupPro.Tests.Binders
{
    public class TrimmingRulesTests
    {
        [Theory]
        [InlineData("Password")]
        [InlineData("ConfirmPassword")]
        [InlineData("CurrentPassword")]
        [InlineData("AdminPassword")]
        [InlineData("password")]
        [InlineData("PASSWORD")]
        public void ShouldSkipTrim_PasswordLikeProperty_ReturnsTrue(string propertyName)
        {
            Assert.True(TrimmingRules.ShouldSkipTrim(propertyName));
        }

        [Theory]
        [InlineData("UserName")]
        [InlineData("CompanyName")]
        [InlineData("Email")]
        [InlineData("ConfirmDatabaseName")]
        [InlineData("")]
        public void ShouldSkipTrim_RegularProperty_ReturnsFalse(string propertyName)
        {
            Assert.False(TrimmingRules.ShouldSkipTrim(propertyName));
        }

        [Fact]
        public void ShouldSkipTrim_NullPropertyName_ReturnsFalse()
        {
            Assert.False(TrimmingRules.ShouldSkipTrim(null));
        }
    }
}
