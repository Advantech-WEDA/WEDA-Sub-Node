namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// Topic grammar for CFX messages bridged onto MQTT.
/// </summary>
/// <remarks>
/// <para>
/// A bridged CFX topic is built from the publishing endpoint's CFX handle and the message's
/// position in the CFX type namespace:
/// </para>
/// <code>
/// {Vendor}/{Model}/{Serial}/CFX/[&lt;namespace path&gt;/]{MessageName}
/// └──── CFX handle, dots replaced by slashes ────┘
/// </code>
/// <para>
/// For example CFX handle <c>SUNJSONG.SLD880A.0001</c> publishing
/// <c>CFX.ResourcePerformance.StationStateChanged</c> yields
/// <c>SUNJSONG/SLD880A/0001/CFX/ResourcePerformance/StationStateChanged</c>.
/// </para>
/// <para>
/// The namespace path is between zero and two segments deep depending on the message
/// (<c>CFX.EndpointConnected</c> has none; <c>CFX.Production.Hermes.MagazineArrived</c> has two),
/// so topic depth is not fixed. Subscribers therefore match with a multi-level wildcard and
/// resolve the message type from the envelope's <c>MessageName</c> rather than from the topic —
/// the envelope is the authoritative source, the topic is a routing convenience.
/// </para>
/// </remarks>
public static class CfxTopic
{
    /// <summary>Topic segment separating the CFX handle from the CFX message path.</summary>
    public const string DefaultRoot = "CFX";

    /// <summary>MQTT single-level wildcard.</summary>
    private const string SingleLevel = "+";

    /// <summary>MQTT multi-level wildcard.</summary>
    private const string MultiLevel = "#";

    /// <summary>
    /// Converts a CFX handle into its topic prefix by replacing the dot separators with slashes.
    /// </summary>
    /// <param name="cfxHandle">
    /// Dot-separated CFX handle, conventionally <c>{Vendor}.{Model}.{Serial}</c>
    /// (for example <c>SUNJSONG.SLD880A.0001</c>).
    /// </param>
    /// <returns>The slash-separated topic prefix (for example <c>SUNJSONG/SLD880A/0001</c>).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cfxHandle"/> is null or blank.</exception>
    public static string ToTopicPrefix(string cfxHandle)
    {
        if (string.IsNullOrWhiteSpace(cfxHandle))
        {
            throw new ArgumentException("CFX handle must not be null or blank.", nameof(cfxHandle));
        }

        return cfxHandle.Trim().Replace('.', '/');
    }

    /// <summary>
    /// Builds the subscription filter covering every CFX message published by one endpoint.
    /// </summary>
    /// <param name="cfxHandle">The endpoint's CFX handle.</param>
    /// <param name="root">Topic root segment; defaults to <see cref="DefaultRoot"/>.</param>
    /// <returns>For example <c>SUNJSONG/SLD880A/0001/CFX/#</c>.</returns>
    public static string SubscriptionFilterFor(string cfxHandle, string root = DefaultRoot) =>
        $"{ToTopicPrefix(cfxHandle)}/{root}/{MultiLevel}";

    /// <summary>
    /// Builds the subscription filter covering every CFX message from every endpoint whose handle
    /// has <paramref name="handleSegments"/> segments.
    /// </summary>
    /// <param name="handleSegments">
    /// Number of segments in the CFX handles to match. Defaults to 3 for the conventional
    /// <c>{Vendor}.{Model}.{Serial}</c> form.
    /// </param>
    /// <param name="root">Topic root segment; defaults to <see cref="DefaultRoot"/>.</param>
    /// <returns>For example <c>+/+/+/CFX/#</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="handleSegments"/> is less than 1.
    /// </exception>
    public static string SubscriptionFilterForAnyEndpoint(int handleSegments = 3, string root = DefaultRoot)
    {
        if (handleSegments < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(handleSegments),
                handleSegments,
                "A CFX handle has at least one segment.");
        }

        var prefix = string.Join('/', Enumerable.Repeat(SingleLevel, handleSegments));
        return $"{prefix}/{root}/{MultiLevel}";
    }

    /// <summary>
    /// Best-effort extraction of the CFX handle from a received topic.
    /// </summary>
    /// <remarks>
    /// Used for diagnostics and for correlating messages to endpoints when the envelope's
    /// <c>Source</c> field is absent. Prefer the envelope's <c>Source</c> when it is populated.
    /// </remarks>
    /// <param name="topic">The topic the message arrived on.</param>
    /// <param name="cfxHandle">The dot-separated handle, when the topic contains the root segment.</param>
    /// <param name="root">Topic root segment; defaults to <see cref="DefaultRoot"/>.</param>
    /// <returns><c>true</c> when a handle could be extracted.</returns>
    public static bool TryParseHandle(
        string? topic,
        out string cfxHandle,
        string root = DefaultRoot)
    {
        cfxHandle = string.Empty;

        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rootIndex = Array.FindIndex(segments, s => s.Equals(root, StringComparison.Ordinal));

        // The root must appear after at least one handle segment.
        if (rootIndex < 1)
        {
            return false;
        }

        cfxHandle = string.Join('.', segments[..rootIndex]);
        return true;
    }
}
