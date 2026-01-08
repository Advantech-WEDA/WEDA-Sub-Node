using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Tcp;
using Weda.SubNode.Core.Communication.WebSocket;

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

        /// <summary>
        /// WebSocket communication factory.
        /// </summary>
        public static class WebSocket
        {
            /// <summary>
            /// Creates a WebSocket communication instance with default settings (ws://localhost:8080).
            /// </summary>
            public static WebSocketCommunication Default =>
                Create("ws://localhost:8080");

            /// <summary>
            /// Creates a WebSocket communication instance with specified URI.
            /// </summary>
            /// <param name="uri">WebSocket URI (ws:// or wss://)</param>
            /// <param name="settings">Connection settings</param>
            public static WebSocketCommunication Create(
                string uri,
                ConnectionSettings? settings = null)
            {
                var logger = _loggerFactory?.CreateLogger<CommunicationBase>();
                return new WebSocketCommunication(uri, settings, logger);
            }

            /// <summary>
            /// Creates a WebSocket communication instance with host, port, and path.
            /// </summary>
            /// <param name="host">Host address</param>
            /// <param name="port">Port number</param>
            /// <param name="path">Path (default: /)</param>
            /// <param name="secure">Use wss:// instead of ws:// (default: false)</param>
            /// <param name="settings">Connection settings</param>
            public static WebSocketCommunication Create(
                string host,
                int port,
                string path = "/",
                bool secure = false,
                ConnectionSettings? settings = null)
            {
                var scheme = secure ? "wss" : "ws";
                var uri = $"{scheme}://{host}:{port}{path}";
                var logger = _loggerFactory?.CreateLogger<CommunicationBase>();
                return new WebSocketCommunication(uri, settings, logger);
            }
        }
    }
}
