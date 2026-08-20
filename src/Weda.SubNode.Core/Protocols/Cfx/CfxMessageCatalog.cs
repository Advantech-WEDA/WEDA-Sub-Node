using System.Text;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// The CFX messages this SubNode subscribes to, and the sensor naming convention applied to them.
/// </summary>
/// <remarks>
/// <para>
/// CFX defines several hundred message types. This catalogue lists the subset a bridged endpoint
/// actually emits in the reference deployment — the messages that can be captured passively,
/// without the SubNode acting as a CFX request/response participant.
/// </para>
/// <para>
/// The catalogue is advisory: the parser will surface any message that a sensor is configured for.
/// Its purpose is to provide a default sensor set and to let configuration be validated against a
/// known-good list, so a typo in a message name fails at startup rather than silently producing a
/// sensor that never reports.
/// </para>
/// <para>
/// CFX does not define the order in which an endpoint emits these messages — the specification only
/// says under which circumstances each is sent. Consumers must not assume a fixed sequence, because
/// it varies between equipment vendors.
/// </para>
/// </remarks>
public static class CfxMessageCatalog
{
    /// <summary>
    /// Fully-qualified CFX message names captured in the reference deployment, in the nominal
    /// order of a production flow (connect, load, work, inspect, unload, depart, disconnect).
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedMessageNames =
    [
        "CFX.EndpointConnected",
        "CFX.ResourcePerformance.StationOnline",
        "CFX.Production.Hermes.MagazineArrived",
        "CFX.Production.UnitsArrived",
        "CFX.Sensor.Identification.IdentifiersRead",
        "CFX.Production.UnitsInitialized",
        "CFX.Production.LoadingAndUnloading.UnitsLoaded",
        "CFX.Production.WorkStarted",
        "CFX.Production.TestAndInspection.UnitsInspected",
        "CFX.Production.WorkCompleted",
        "CFX.Production.LoadingAndUnloading.UnitsUnloaded",
        "CFX.Production.UnitsDeparted",
        "CFX.Production.Hermes.MagazineDeparted",
        "CFX.EndpointShuttingDown",
        "CFX.ResourcePerformance.StationStateChanged",
        "CFX.ResourcePerformance.FaultOccurred",
        "CFX.ResourcePerformance.FaultCleared",
    ];

    private static readonly HashSet<string> SupportedLookup =
        new(SupportedMessageNames, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="messageName"/> is in the catalogue.
    /// </summary>
    public static bool IsSupported(string? messageName) =>
        !string.IsNullOrWhiteSpace(messageName) && SupportedLookup.Contains(messageName);

    /// <summary>
    /// Derives the default sensor name for a CFX message: the unqualified message name converted
    /// to snake_case and prefixed with <c>cfx_</c>.
    /// </summary>
    /// <example>
    /// <c>CFX.ResourcePerformance.StationStateChanged</c> becomes <c>cfx_station_state_changed</c>.
    /// </example>
    /// <param name="messageName">Fully-qualified CFX message name.</param>
    /// <returns>The default sensor name.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="messageName"/> is null or blank.</exception>
    public static string ToSensorName(string messageName)
    {
        if (string.IsNullOrWhiteSpace(messageName))
        {
            throw new ArgumentException("CFX message name must not be null or blank.", nameof(messageName));
        }

        var leaf = messageName.AsSpan()[(messageName.LastIndexOf('.') + 1)..];
        var builder = new StringBuilder("cfx_", leaf.Length + 8);

        for (var i = 0; i < leaf.Length; i++)
        {
            var c = leaf[i];

            // Break before each capital that starts a new word, so runs of capitals such as the
            // "ID" in "TransactionID" do not become "i_d".
            var startsWord = char.IsUpper(c)
                && i > 0
                && (!char.IsUpper(leaf[i - 1]) || (i + 1 < leaf.Length && char.IsLower(leaf[i + 1])));

            if (startsWord)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Derives the topic path a message is published under, relative to the topic root.
    /// </summary>
    /// <example>
    /// <c>CFX.Production.Hermes.MagazineArrived</c> becomes <c>Production/Hermes/MagazineArrived</c>.
    /// </example>
    /// <param name="messageName">Fully-qualified CFX message name.</param>
    /// <returns>The slash-separated path below the <c>CFX</c> root segment.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="messageName"/> is null, blank, or not <c>CFX</c>-qualified.
    /// </exception>
    public static string ToRelativeTopicPath(string messageName)
    {
        if (string.IsNullOrWhiteSpace(messageName))
        {
            throw new ArgumentException("CFX message name must not be null or blank.", nameof(messageName));
        }

        const string prefix = "CFX.";
        if (!messageName.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"CFX message name '{messageName}' must be qualified with the 'CFX.' namespace.",
                nameof(messageName));
        }

        return messageName[prefix.Length..].Replace('.', '/');
    }
}
