using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Core.Cloud;

namespace Weda.SubNode.Core;

/// <summary>
/// Cloud service factories (Core project part).
/// </summary>
public static partial class WedaFactory
{
    public static class Cloud
    {
        /// <summary>
        /// Default cloud service instance.
        /// In Core project, this returns Null cloud service.
        /// The Weda.SubNode.Cloud project extends this with a real NATS cloud service.
        /// </summary>
        public static IWedaCloudService Default => Null;

        /// <summary>
        /// Null cloud service for testing and offline scenarios.
        /// Does not connect to any cloud, generates mock deviceId locally.
        /// </summary>
        public static IWedaCloudService Null
        {
            get
            {
                var logger = _loggerFactory?.CreateLogger<NullCloudService>();
                return new NullCloudService(logger);
            }
        }

        /// <summary>
        /// Alias for Null - mock cloud service for testing.
        /// </summary>
        public static IWedaCloudService Mock => Null;
    }
}
