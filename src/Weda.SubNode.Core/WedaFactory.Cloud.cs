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
        /// In Core project, this returns Mock cloud service.
        /// The Weda.SubNode.Cloud project extends this with a real NATS cloud service.
        /// </summary>
        public static IWedaCloudService Default => Mock;

        /// <summary>
        /// Mock cloud service for testing and offline scenarios.
        /// Does not connect to any cloud, generates mock deviceId locally.
        /// </summary>
        public static IWedaCloudService Mock
        {
            get
            {
                var logger = _loggerFactory?.CreateLogger<MockCloudService>();
                return new MockCloudService(logger);
            }
        }
    }
}
