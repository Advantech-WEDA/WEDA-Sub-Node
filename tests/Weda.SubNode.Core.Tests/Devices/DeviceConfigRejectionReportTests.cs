using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.TestBase;
using Weda.SubNode.TestBase.Builders;

using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

/// <summary>
/// Reproduction tests for Issue 54293: a devicecfg update the device refuses must be
/// reported back to the shadow as "invalid" instead of being abandoned silently.
///
/// A derived device signals refusal by throwing from <c>OnBeforeConfigUpdateAsync</c>
/// (the documented derived-class validation hook — see the system-agent example).
/// Phase 1 must translate that refusal into <see cref="ConfigUpdateValidationResult.Invalid"/>
/// so SubNodeManager can publish the aggregated invalid report.
/// </summary>
public class DeviceConfigRejectionReportTests : IDisposable
{
    private const string RejectionReason = "Sensor 'cpu' interval 10 is below the minimum of 1000";

    private readonly MockApplicationContext _context;
    private readonly IProtocolParserCore _mockProtocolParser;
    private readonly DeviceConfiguration _testConfig;

    public DeviceConfigRejectionReportTests()
    {
        _context = new MockApplicationContext();

        _mockProtocolParser = Substitute.For<IProtocolParserCore>();
        _mockProtocolParser.Communication.Returns(_context.MockCommunication);

        _testConfig = DeviceConfigurationBuilder.Default()
            .WithDeviceName("test-device")
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502
            })
            .Build();
    }

    public void Dispose() => _context?.Dispose();

    [Fact]
    public async Task ValidateConfigurationUpdateAsync_WhenRefusalHookThrows_ShouldReturnInvalidInsteadOfPropagating()
    {
        // Arrange
        var device = new RejectingConfigDevice(_context, _testConfig, _mockProtocolParser)
        {
            RejectionMessage = RejectionReason
        };

        var message = CreateDeviceConfigMessage(_testConfig.DeviceName);

        // Act
        var result = await device.ValidateConfigurationUpdateAsync(message, CancellationToken.None);

        // Assert - the refusal becomes a verdict, not an escaping exception
        result.IsValid.ShouldBeFalse("A refused configuration must fail validation");
        result.IsSkipped.ShouldBeFalse("A refused configuration is not a skip");
        result.ErrorMessage.ShouldBe(RejectionReason,
            "The device's own rejection reason must survive into the report");
    }

    [Fact]
    public async Task ValidateConfigurationUpdateAsync_WhenRefusalHookThrows_ShouldReleaseLockForNextUpdate()
    {
        // Arrange
        var device = new RejectingConfigDevice(_context, _testConfig, _mockProtocolParser)
        {
            RejectionMessage = RejectionReason
        };

        var message = CreateDeviceConfigMessage(_testConfig.DeviceName);

        // Act - a rejection followed by an accepted update (AC: invalid is not sticky)
        var rejected = await device.ValidateConfigurationUpdateAsync(message, CancellationToken.None);

        device.RejectionMessage = null;
        var accepted = await device.ValidateConfigurationUpdateAsync(message, CancellationToken.None);

        // Assert
        rejected.IsValid.ShouldBeFalse();
        accepted.IsValid.ShouldBeTrue("A later valid update must still be accepted after a rejection");
    }

    private static SubNodeConfigUpdateMessage CreateDeviceConfigMessage(string deviceName)
    {
        var deviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>(StringComparer.OrdinalIgnoreCase)
        {
            [deviceName] = new SubNodeDeviceConfigDto { Sensors = new List<SubNodeSensorReportDto>() }
        };

        return new SubNodeConfigUpdateMessage
        {
            SeqId = 1,
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = new SubNodeDesiredConfigSections
                    {
                        DeviceCfg = new SubNodeDeviceCfgDto { DeviceConfigs = deviceConfigs }
                    }
                }
            }
        };
    }
}

/// <summary>
/// Device whose derived-class validation hook refuses a configuration by throwing,
/// the way the system-agent example does.
/// </summary>
internal sealed class RejectingConfigDevice : DeviceBase
{
    public RejectingConfigDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IProtocolParserCore protocolParser)
        : base(context, configuration, protocolParser)
    {
    }

    /// <summary>
    /// Reason to refuse the incoming configuration with; null accepts it.
    /// </summary>
    public string? RejectionMessage { get; set; }

    public override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<TelemetryMeasure>());

    protected override Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
        List<string> sensorResourceIds,
        CancellationToken cancellationToken)
        => Task.FromResult(new IntervalGroupReadResult(new List<TelemetryMeasure>(), TimeSpan.Zero));

    protected override Task OnBeforeConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        if (RejectionMessage != null)
            throw new InvalidOperationException(RejectionMessage);

        return base.OnBeforeConfigUpdateAsync(e, ct);
    }
}
