using BackupPro.Services;
using Xunit;

namespace BackupPro.Tests.Services
{
    public class SetupStateTests
    {
        [Theory]
        [InlineData("admin")]
        [InlineData("Admin")]
        [InlineData("ADMIN")]
        public void IsBootstrapAdmin_SingleAdminUser_ReturnsTrue(string userName)
        {
            Assert.True(SetupState.IsBootstrapAdmin(totalUsers: 1, userName));
        }

        [Fact]
        public void IsBootstrapAdmin_MoreThanOneUser_ReturnsFalse()
        {
            Assert.False(SetupState.IsBootstrapAdmin(totalUsers: 2, "admin"));
        }

        [Fact]
        public void IsBootstrapAdmin_DifferentUserName_ReturnsFalse()
        {
            Assert.False(SetupState.IsBootstrapAdmin(totalUsers: 1, "carlos"));
        }

        [Fact]
        public void IsBootstrapAdmin_NullUserName_ReturnsFalse()
        {
            Assert.False(SetupState.IsBootstrapAdmin(totalUsers: 1, null));
        }

        [Fact]
        public void IsBootstrapAdmin_NoUsers_ReturnsFalse()
        {
            Assert.False(SetupState.IsBootstrapAdmin(totalUsers: 0, "admin"));
        }
    }
}
