using System.Runtime.CompilerServices;
using Advantech.Edge.Common;
using Advantech.Edge.Daq;
using Advantech.Edge.Daq.Module;
using Advantech.Edge.Daq.Module.Analog;
using daq_data_collector.Models;
using Microsoft.Extensions.Logging;

namespace daq_data_collector.Communication;

/// <summary>
/// Collects raw vibration samples from hardware via Advantech.Edge.Daq streaming.
/// Uses IStreamer.StreamAsync() (await foreach) to consume hardware buffers,
/// accumulates samples until FrameSize is reached, then yields a complete
/// DaqRawFrame through an async enumerable.
///
/// API chain:
///   DaqModuleManager → CreateDaqModule()
///   → DaqModule.Analog.Channels[i].ConfigureStreamer().Build() → IStreamer
///   → await foreach (buffer in streamer.StreamAsync())
///   → accumulate → yield DaqRawFrame
/// </summary>
public class DaqCollector
{
    private readonly int _samplingRate;
    private readonly int _frameSize;  // Calculated from SamplingRate * FrameIntervalSeconds
    private readonly int _daqModuleDeviceNumber;
    private readonly ILogger<DaqCollector> _logger;
    private DaqModuleManager? _manager;

    public DaqCollector(
        int samplingRate,
        int frameSize,
        int daqModuleDeviceNumber,
        ILogger<DaqCollector> logger)
    {
        if (samplingRate <= 0)
            throw new ArgumentException("SamplingRate must be positive.", nameof(samplingRate));
        if (frameSize <= 0)
            throw new ArgumentException("FrameSize must be positive.", nameof(frameSize));
        if (daqModuleDeviceNumber < 0)
            throw new ArgumentException("DaqModuleDeviceNumber must be non-negative.", nameof(daqModuleDeviceNumber));

        _samplingRate = samplingRate;
        _frameSize = frameSize;
        _daqModuleDeviceNumber = daqModuleDeviceNumber;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or creates the DaqModuleManager instance (singleton per collector instance).
    /// </summary>
    private DaqModuleManager GetDaqModuleManager(ILogger? logger = null)
    {
        return _manager ??= new DaqModuleManager(logger);
    }

    /// <summary>
    /// Checks if DAQ modules are available and can be discovered.
    /// Verifies that the target DAQ module (with matching DeviceNumber) exists.
    /// Returns true if the target DAQ module is found, false otherwise.
    /// </summary>
    public bool VerifyDaqModulesAvailable()
    {
        try
        {
            var manager = GetDaqModuleManager();

            _logger.LogInformation("Discovering DAQ modules for verification...");

            var moduleInfos = manager.DiscoverDaqModules();

            if (!moduleInfos.Any())
            {
                _logger.LogError("No DAQ modules discovered during verification.");
                return false;
            }

            _logger.LogInformation("Discovered {count} DAQ module(s) during verification.", moduleInfos.Count());

            // Verify that the target module with matching DeviceNumber exists
            var targetModule = moduleInfos.FirstOrDefault(m => m.DeviceNumber == _daqModuleDeviceNumber);
            if (targetModule == null)
            {
                _logger.LogError("Target DAQ module with DeviceNumber={device} not found. Available devices: {devices}",
                    _daqModuleDeviceNumber,
                    string.Join(", ", moduleInfos.Select(m => m.DeviceNumber)));
                return false;
            }

            _logger.LogInformation("DAQ module verification successful: Target module DeviceNumber={device} found.", _daqModuleDeviceNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying DAQ module availability.");
            return false;
        }
    }

    /// <summary>
    /// Streams raw vibration frames from the configured DAQ channel.
    /// Runs until <paramref name="cancellationToken"/> is cancelled.
    /// Yields one DaqRawFrame for each FrameSize samples accumulated.
    /// </summary>
    public IAsyncEnumerable<DaqRawFrame> StreamFramesAsync(
        CancellationToken cancellationToken = default)
    {
        return StreamFramesAsyncImpl(cancellationToken);
    }

    private async IAsyncEnumerable<DaqRawFrame> StreamFramesAsyncImpl(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting DAQ streaming: DaqDeviceNumber={device}, SamplingRate={rate} Hz, FrameSize={frameSize}",
            _daqModuleDeviceNumber, _samplingRate, _frameSize);

        // Get DAQ module manager (singleton instance)
        var manager = GetDaqModuleManager(_logger);

        _logger.LogInformation("Discovering DAQ modules...");

        // Discover available DAQ modules
        var moduleInfos = manager.DiscoverDaqModules();
        if (!moduleInfos.Any())
        {
            _logger.LogError("No DAQ modules discovered.");
            yield break;
        }

        _logger.LogInformation("Discovered {count} DAQ modules.", moduleInfos.Count());

        // Find the target module by device number
        var targetModuleInfo = moduleInfos.FirstOrDefault(m => m.DeviceNumber == _daqModuleDeviceNumber);
        if (targetModuleInfo == null)
        {
            _logger.LogError("Target DAQ module (device {device}) not found.", _daqModuleDeviceNumber);
            yield break;
        }

        _logger.LogInformation($"Found target DAQ module: DeviceNumber={targetModuleInfo.DeviceNumber}");

        // Create DAQ module instance
        DaqModule module;
        try
        {
            _logger.LogInformation("Creating DAQ module instance for device {device}...", _daqModuleDeviceNumber);
            module = manager.CreateDaqModule(targetModuleInfo, DaqModuleAccessMode.Writable);
            _logger.LogInformation("Created DAQ module instance for device {device}.", _daqModuleDeviceNumber);
            if (module.Analog == null)
            {
                _logger.LogError("DAQ module does not support analog input.");
                yield break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating DAQ module instance for device {device}.", _daqModuleDeviceNumber);
            throw;
        }

        _logger.LogInformation("Successfully created DAQ module instance for device {device}.", _daqModuleDeviceNumber);

        // Verify streaming support
        if (!module.Analog.Capabilities.InputStreamSupported)
        {
            _logger.LogError("DAQ module does not support analog input streaming.");
            yield break;
        }

        // Configure analog input settings
        module.Analog.Input.Configuration.SampleClockSource = SampleClockSource.BackplaneClock;
        module.Analog.Input.Configuration.SampleInterval = TimeSpan.FromMicroseconds(1_000_000.0 / _samplingRate);

        _logger.LogInformation("Configured DAQ module for streaming: SampleClockSource=BackplaneClock, SampleInterval={interval} ms",
            module.Analog.Input.Configuration.SampleInterval.TotalMilliseconds);

        // Configure and start streamer for X-axis (channel 0) only
        const int targetChannel = 0;
        var streamer = module.Analog.Input.ConfigureStreamer()
            .WithScanRange(new ChannelScanRange(StartChannel: targetChannel, EndChannel: targetChannel))
            .WithReportInterval(TimeSpan.FromSeconds(_frameIntervalSeconds))
            .Build();

        // Accumulate samples and yield frames
        var samples = new List<float>(_frameSize);
        var frameStartTime = DateTimeOffset.UtcNow;

        // Stream reports asynchronously until cancellation is requested
        await foreach (var report in streamer.StreamAsync(cancellationToken))
        {
            // Extract samples for target channel
            var channelSamples = report.ExtractChannelSamples(channelIndex: targetChannel);

            _logger.LogInformation("Received report with {sampleCount} samples for channel {channel}.", channelSamples.Length, targetChannel);

            foreach (var sample in channelSamples)
            {
                samples.Add((float)sample.Value);

                // When we have a full frame, yield it
                if (samples.Count >= _frameSize)
                {
                    var frame = new DaqRawFrame
                    {
                        Samples = samples.Take(_frameSize).ToArray(),
                        Timestamp = frameStartTime,
                        AxisName = "X",  // Hard-coded for single-axis accelerometer
                        SamplingRate = _samplingRate
                    };

                    _logger.LogDebug("Yielding frame {frame}: {samples} samples", samples.Count / _frameSize, _frameSize);

                    yield return frame;

                    // Remove processed samples and reset timestamp for next frame
                    samples.RemoveRange(0, _frameSize);
                    frameStartTime = DateTimeOffset.UtcNow;
                }
            }
        }

        _logger.LogInformation("DAQ streaming stopped.");
    }
}
