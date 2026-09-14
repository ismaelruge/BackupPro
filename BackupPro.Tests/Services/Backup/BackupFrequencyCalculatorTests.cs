using BackupPro.Services.Backup;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class BackupFrequencyCalculatorTests
    {
        private static readonly DateTime BaseTime = new(2026, 1, 1, 10, 0, 0);

        [Theory]
        [InlineData("minutes", 30)]
        [InlineData("Minutes", 30)]
        [InlineData("MINUTES", 30)]
        public void CalculateNextRun_Minutes_AddsMinutes(string frequencyType, int value)
        {
            var result = BackupFrequencyCalculator.CalculateNextRun(frequencyType, value, BaseTime);

            Assert.Equal(BaseTime.AddMinutes(value), result);
        }

        [Theory]
        [InlineData("hours", 6)]
        [InlineData("Hours", 6)]
        public void CalculateNextRun_Hours_AddsHours(string frequencyType, int value)
        {
            var result = BackupFrequencyCalculator.CalculateNextRun(frequencyType, value, BaseTime);

            Assert.Equal(BaseTime.AddHours(value), result);
        }

        [Theory]
        [InlineData("days", 2)]
        [InlineData("Days", 2)]
        public void CalculateNextRun_Days_AddsDays(string frequencyType, int value)
        {
            var result = BackupFrequencyCalculator.CalculateNextRun(frequencyType, value, BaseTime);

            Assert.Equal(BaseTime.AddDays(value), result);
        }

        [Fact]
        public void CalculateNextRun_UnknownFrequency_DefaultsToOneHour()
        {
            var result = BackupFrequencyCalculator.CalculateNextRun("weeks", 3, BaseTime);

            Assert.Equal(BaseTime.AddHours(1), result);
        }

        [Fact]
        public void CalculateNextRun_EmptyFrequency_DefaultsToOneHour()
        {
            var result = BackupFrequencyCalculator.CalculateNextRun(string.Empty, 3, BaseTime);

            Assert.Equal(BaseTime.AddHours(1), result);
        }
    }
}
