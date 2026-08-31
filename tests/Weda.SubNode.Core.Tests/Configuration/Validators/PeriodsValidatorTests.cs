using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Configuration.Validators.Device;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for PeriodsValidator ReportConfiguration bounds (US-47107 AC-2).
/// </summary>
public class PeriodsValidatorTests
{
    private readonly PeriodsValidator _validator = new();

    [Theory]
    [InlineData(60_000)]        // min boundary (1 minute)
    [InlineData(86_400_000)]    // default (24 hours)
    [InlineData(604_800_000)]   // max boundary (7 days)
    [InlineData(0)]             // disabled
    public void Validate_Should_AcceptInRangeOrDisabledPeriods(int periodMs)
    {
        // Arrange
        var context = CreateContext(periodMs);

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeTrue(
            $"ReportConfiguration {periodMs}ms should be accepted");
    }

    [Theory]
    [InlineData(59_999)]        // just below min
    [InlineData(10_000)]        // legacy min, now out of range
    [InlineData(604_800_001)]   // just above max
    [InlineData(-1)]            // negative
    public void Validate_Should_RejectOutOfRangePeriods(int periodMs)
    {
        // Arrange
        var context = CreateContext(periodMs);

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeFalse(
            $"ReportConfiguration {periodMs}ms should be rejected");
        result.ErrorMessage!.ShouldContain("ReportConfiguration");
    }

    [Fact]
    public void Validate_Should_Skip_WhenPeriodsNotProvided()
    {
        // Arrange
        var context = new ConfigurationValidationContext
        {
            DesiredConfig = new SubNodeDeviceConfigDto(),
            CurrentConfig = new DeviceConfiguration(),
            Options = new ConfigUpdateOptions()
        };

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeTrue("missing Periods section should skip validation");
    }

    private static ConfigurationValidationContext CreateContext(int reportConfiguration)
    {
        return new ConfigurationValidationContext
        {
            DesiredConfig = new SubNodeDeviceConfigDto
            {
                Periods = new SubNodePeriodsDto { ReportConfiguration = reportConfiguration }
            },
            CurrentConfig = new DeviceConfiguration(),
            Options = new ConfigUpdateOptions()
        };
    }
}
