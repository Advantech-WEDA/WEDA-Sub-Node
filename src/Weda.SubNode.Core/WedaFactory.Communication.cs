using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication;

namespace Weda.SubNode.Core;

/// <summary>
/// Unified factory for Weda SubNode SDK.
/// Use <see cref="UseLoggerFactory"/> to configure logging for all factory methods.
/// </summary>
public static partial class WedaFactory
{
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Configures the logger factory for all WedaFactory methods.
    /// Call this once at application startup.
    /// </summary>
    public static void UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public static class Logger
    {
        /// <summary>
        /// Default logger instance (NullLogger - no output).
        /// </summary>
        public static ILogger Default => NullLogger.Instance;

        /// <summary>
        /// Get typed logger from configured factory.
        /// If no factory is configured, returns NullLogger.
        /// </summary>
        public static ILogger<T> Create<T>()
        {
            return _loggerFactory?.CreateLogger<T>()
                ?? NullLoggerFactory.Instance.CreateLogger<T>();
        }
    }

    public static class Communication
    {
        /// <summary>
        /// TCP communication factory.
        /// </summary>
        public static class Tcp
        {
            /// <summary>
            /// Creates a TCP communication instance with default settings (localhost:502).
            /// </summary>
            public static TcpCommunication Default =>
                Create("localhost", 502);

            /// <summary>
            /// Creates a TCP communication instance with specified host and port.
            /// </summary>
            public static TcpCommunication Create(
                string host,
                int port,
                ConnectionSettings? settings = null)
            {
                var logger = _loggerFactory?.CreateLogger<CommunicationBase>();
                return new TcpCommunication(host, port, settings, logger);
            }
        }
    }
}
