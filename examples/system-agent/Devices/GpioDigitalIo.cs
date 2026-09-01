using Microsoft.Extensions.Logging;

namespace SystemAgentExample.Devices;

/// <summary>
/// Digital IO logic (direction guard + write-then-verify) decoupled from the
/// communication chain via delegates, so it is unit-testable without hardware.
/// </summary>
public class GpioDigitalIo(
    Func<string, bool?> getLevel,
    Func<string, bool, bool> setLevel,
    Func<string, string?> getDirection,
    ILogger logger,
    int verifyMaxRetries = 3,
    TimeSpan? verifyDelay = null)
{
    private readonly TimeSpan _verifyDelay = verifyDelay ?? TimeSpan.FromMilliseconds(50);

    public async Task<bool> SetOutputAsync(string pinName, bool state, CancellationToken ct = default)
    {
        var direction = getDirection(pinName);
        if (direction != "output")
        {
            logger.LogWarning(
                "GPIO pin '{PinName}' is not an output (direction={Direction}), refusing to set",
                pinName, direction ?? "unknown");
            return false;
        }

        if (!setLevel(pinName, state))
            return false;

        for (var attempt = 0; attempt < verifyMaxRetries; attempt++)
        {
            if (getLevel(pinName) == state)
                return true;
            await Task.Delay(_verifyDelay, ct);
        }

        logger.LogWarning(
            "GPIO pin '{PinName}' readback mismatch after set (desired={Desired})",
            pinName, state);
        return false;
    }

    public bool? GetLevel(string pinName) => getLevel(pinName);
}
