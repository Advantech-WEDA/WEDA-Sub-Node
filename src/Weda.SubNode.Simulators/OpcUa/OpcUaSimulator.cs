using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using Weda.SubNode.Simulators.Modbus;

namespace Weda.SubNode.Simulators.OpcUa;

/// <summary>
/// OPC-UA server simulator.
/// Simulates an OPC-UA server with configurable variable nodes that produce dynamic values.
/// Uses the OPC Foundation Server SDK for full OPC-UA compliance.
/// </summary>
public class OpcUaSimulator : IDisposable
{
    private readonly ILogger<OpcUaSimulator> _logger;
    private readonly OpcUaSimulatorConfiguration _configuration;
    private ApplicationInstance? _application;
    private SimulatorServer? _server;
    private SimulatorNodeManager? _nodeManager;
    private CancellationTokenSource? _cts;
    private Task? _simulationTask;
    private bool _disposed;

    public OpcUaSimulator(
        OpcUaSimulatorConfiguration configuration,
        ILogger<OpcUaSimulator>? logger = null)
    {
        _configuration = configuration;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<OpcUaSimulator>();
    }

    /// <summary>
    /// Start the OPC-UA server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_server != null)
            throw new InvalidOperationException("Simulator already started");

        _logger.LogInformation(
            "Starting OPC-UA Simulator on port {Port} (Server: {ServerName})",
            _configuration.Server.Port,
            _configuration.Server.ServerName);

        // Build application configuration
        var appConfig = BuildApplicationConfiguration();

        // Create and start the server
        _application = new ApplicationInstance
        {
            ApplicationName = _configuration.Server.ServerName,
            ApplicationType = ApplicationType.Server,
            ApplicationConfiguration = appConfig
        };

        // Check application certificate
        var hasAppCert = await _application.CheckApplicationInstanceCertificatesAsync(silent: true);

        if (!hasAppCert)
        {
            _logger.LogInformation("Application certificate created for OPC-UA simulator");
        }

        // Create custom server (node manager is created lazily inside CreateMasterNodeManager)
        _server = new SimulatorServer(_configuration, _logger);

        await _application.Start(_server);

        // Retrieve the node manager created during server startup
        _nodeManager = _server.NodeManager;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _simulationTask = Task.Run(() => SimulateNodeValuesAsync(_cts.Token), _cts.Token);

        var endpointUrl = $"opc.tcp://localhost:{_configuration.Server.Port}/{_configuration.Server.ServerName}";
        _logger.LogInformation("OPC-UA Simulator started successfully at {EndpointUrl}", endpointUrl);
        _logger.LogInformation("────────────────────────────────────────────────────────");
    }

    /// <summary>
    /// Stop the OPC-UA server
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping OPC-UA Simulator");

        _cts?.Cancel();

        if (_simulationTask != null)
        {
            try { await _simulationTask; }
            catch (OperationCanceledException) { }
        }

        _server?.Stop();
        _server = null;
        _application = null;

        _logger.LogInformation("OPC-UA Simulator stopped");
    }

    /// <summary>
    /// Simulate node value changes using random walk (same algorithm as Modbus simulator)
    /// </summary>
    private async Task SimulateNodeValuesAsync(CancellationToken cancellationToken)
    {
        // Initialize node values
        _nodeManager?.InitializeNodeValues();

        var sensorTasks = _configuration.Nodes.Select(node =>
            SimulateSingleNodeAsync(node, cancellationToken));

        await Task.WhenAll(sensorTasks);
    }

    private async Task SimulateSingleNodeAsync(SimulatedOpcUaNode node, CancellationToken cancellationToken)
    {
        var simParams = node.SimulationParams;
        var defaults = SensorDefaults.GetDefaults(node.Type);

        // Apply defaults if not configured
        if (simParams.MinValue == 0 && simParams.MaxValue == 0)
        {
            simParams.MinValue = defaults.MinValue;
            simParams.MaxValue = defaults.MaxValue;
        }
        if (simParams.ChangeRate == 0) simParams.ChangeRate = defaults.ChangeRate;
        if (simParams.NoiseLevel == 0) simParams.NoiseLevel = defaults.NoiseLevel;

        var initialValue = simParams.InitialValue
            ?? Random.Shared.NextDouble() * (simParams.MaxValue - simParams.MinValue) + simParams.MinValue;

        _nodeManager?.UpdateNodeValue(node.NodeId, initialValue, node.DataType);

        var unit = node.Unit ?? SensorDefaults.GetDefaultUnit(node.Type);
        _logger.LogInformation(
            "Initialized OPC-UA node {Name} ({NodeId}): InitialValue={Value:F2} {Unit}",
            node.Name, node.NodeId, initialValue, unit);

        var updateIntervalSeconds = _configuration.Simulation.GlobalUpdateIntervalSeconds
            ?? simParams.UpdateIntervalSeconds;
        var updateIntervalMs = updateIntervalSeconds * 1000;
        var currentValue = initialValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(updateIntervalMs, cancellationToken);

                if (!_configuration.Simulation.EnableValueChanges)
                    continue;

                // Random walk with boundaries (same algorithm as Modbus simulator)
                var direction = Random.Shared.NextDouble() * 2 - 1;
                var change = direction * simParams.ChangeRate;
                var noise = (Random.Shared.NextDouble() * 2 - 1) * simParams.NoiseLevel;
                currentValue = Math.Clamp(currentValue + change + noise, simParams.MinValue, simParams.MaxValue);

                _nodeManager?.UpdateNodeValue(node.NodeId, currentValue, node.DataType);

                _logger.LogDebug("OPC-UA node {Name} updated: {Value:F2} {Unit}",
                    node.Name, currentValue, unit);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating OPC-UA node {Name}", node.Name);
            }
        }
    }

    private ApplicationConfiguration BuildApplicationConfiguration()
    {
        var serverName = _configuration.Server.ServerName;
        var port = _configuration.Server.Port;

        var config = new ApplicationConfiguration
        {
            ApplicationName = serverName,
            ApplicationType = ApplicationType.Server,
            ApplicationUri = Utils.Format("urn:{0}:{1}", System.Net.Dns.GetHostName(), serverName),
            ProductUri = "urn:Advantech:WedaSubNode:OpcUaSimulator",
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "simulator", "own"),
                    SubjectName = $"CN={serverName}"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "simulator", "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "simulator", "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "simulator", "rejected")
                },
                AutoAcceptUntrustedCertificates = true
            },
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000
            },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = [$"opc.tcp://0.0.0.0:{port}/{serverName}"],
                SecurityPolicies =
                [
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    }
                ],
                UserTokenPolicies =
                [
                    new UserTokenPolicy(UserTokenType.Anonymous)
                ],
                MinRequestThreadCount = 5,
                MaxRequestThreadCount = 100,
                MaxQueuedRequestCount = 2000
            }
        };

        config.Validate(ApplicationType.Server).GetAwaiter().GetResult();
        return config;
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Custom OPC-UA server implementation that creates SimulatorNodeManager lazily
/// during server startup (when IServerInternal is available).
/// </summary>
internal class SimulatorServer : StandardServer
{
    private readonly OpcUaSimulatorConfiguration _configuration;
    private readonly ILogger _logger;

    public SimulatorNodeManager? NodeManager { get; private set; }

    public SimulatorServer(OpcUaSimulatorConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override MasterNodeManager CreateMasterNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration)
    {
        // Create the node manager here where server is available
        NodeManager = new SimulatorNodeManager(server, configuration, _configuration, _logger);

        var nodeManagers = new List<INodeManager> { NodeManager };
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }
}

/// <summary>
/// Custom node manager for the OPC-UA simulator.
/// Creates variable nodes in namespace index 2 based on configuration.
/// </summary>
internal class SimulatorNodeManager : CustomNodeManager2
{
    private readonly OpcUaSimulatorConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly Dictionary<string, BaseDataVariableState> _variables = new();

    public SimulatorNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration,
        OpcUaSimulatorConfiguration simulatorConfig,
        ILogger logger)
        : base(server, configuration, "urn:WedaSubNode:OpcUaSimulator")
    {
        _configuration = simulatorConfig;
        _logger = logger;
    }

    /// <summary>
    /// Creates the NodeId for the instance namespace
    /// </summary>
    public override NodeId New(ISystemContext context, NodeState node)
    {
        return node.BrowseName != null
            ? new NodeId(node.BrowseName.Name, NamespaceIndex)
            : base.New(context, node);
    }

    /// <summary>
    /// Creates the address space (called during server startup)
    /// </summary>
    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            // Get or create external references for the Objects folder
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
            {
                externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
            }

            // Create a folder for our simulated nodes
            var simulatorFolder = new FolderState(null)
            {
                SymbolicName = "Simulator",
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId("Simulator", NamespaceIndex),
                BrowseName = new QualifiedName("Simulator", NamespaceIndex),
                DisplayName = new LocalizedText("Simulator"),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            simulatorFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, simulatorFolder.NodeId));

            simulatorFolder.EventNotifier = EventNotifiers.SubscribeToEvents;
            AddRootNotifier(simulatorFolder);

            // Create variable nodes for each configured sensor
            foreach (var nodeConfig in _configuration.Nodes)
            {
                var variable = CreateVariableNode(simulatorFolder, nodeConfig);
                _variables[nodeConfig.NodeId] = variable;
            }

            // Add all to address space
            AddPredefinedNode(SystemContext, simulatorFolder);

            _logger.LogDebug("OPC-UA address space created with {NodeCount} variable nodes",
                _variables.Count);
        }
    }

    private BaseDataVariableState CreateVariableNode(FolderState parent, SimulatedOpcUaNode nodeConfig)
    {
        var (builtInType, dataTypeId, defaultValue) = GetOpcUaTypeInfo(nodeConfig.DataType);

        var variable = new BaseDataVariableState(parent)
        {
            SymbolicName = nodeConfig.NodeId,
            ReferenceTypeId = ReferenceTypeIds.Organizes,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(nodeConfig.NodeId, NamespaceIndex),
            BrowseName = new QualifiedName(nodeConfig.NodeId, NamespaceIndex),
            DisplayName = new LocalizedText(nodeConfig.Name),
            Description = new LocalizedText($"Simulated {nodeConfig.Type} sensor"),
            DataType = dataTypeId,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = nodeConfig.Writable
                ? AccessLevels.CurrentReadOrWrite
                : AccessLevels.CurrentRead,
            UserAccessLevel = nodeConfig.Writable
                ? AccessLevels.CurrentReadOrWrite
                : AccessLevels.CurrentRead,
            MinimumSamplingInterval = MinimumSamplingIntervals.Continuous,
            Historizing = false,
            Value = defaultValue,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };

        if (nodeConfig.Unit != null)
        {
            variable.Description = new LocalizedText(
                $"Simulated {nodeConfig.Type} sensor ({nodeConfig.Unit})");
        }

        parent.AddChild(variable);
        return variable;
    }

    /// <summary>
    /// Initialize all node values based on configuration
    /// </summary>
    public void InitializeNodeValues()
    {
        foreach (var nodeConfig in _configuration.Nodes)
        {
            var defaults = SensorDefaults.GetDefaults(nodeConfig.Type);
            var simParams = nodeConfig.SimulationParams;

            if (simParams.MinValue == 0 && simParams.MaxValue == 0)
            {
                simParams.MinValue = defaults.MinValue;
                simParams.MaxValue = defaults.MaxValue;
            }

            var initialValue = simParams.InitialValue
                ?? Random.Shared.NextDouble() * (simParams.MaxValue - simParams.MinValue) + simParams.MinValue;

            UpdateNodeValue(nodeConfig.NodeId, initialValue, nodeConfig.DataType);
        }
    }

    /// <summary>
    /// Update a node's value
    /// </summary>
    public void UpdateNodeValue(string nodeId, double value, SimulatedDataType dataType)
    {
        lock (Lock)
        {
            if (!_variables.TryGetValue(nodeId, out var variable))
                return;

            variable.Value = ConvertToTypedValue(value, dataType);
            variable.StatusCode = StatusCodes.Good;
            variable.Timestamp = DateTime.UtcNow;
            variable.ClearChangeMasks(SystemContext, false);
        }
    }

    private static object ConvertToTypedValue(double value, SimulatedDataType dataType)
    {
        return dataType switch
        {
            SimulatedDataType.UInt16 => (ushort)value,
            SimulatedDataType.Int16 => (short)value,
            SimulatedDataType.UInt32 => (uint)value,
            SimulatedDataType.Int32 => (int)value,
            SimulatedDataType.Float32 => (float)value,
            SimulatedDataType.UInt64 => (ulong)value,
            SimulatedDataType.Int64 => (long)value,
            SimulatedDataType.Float64 => value,
            _ => (float)value
        };
    }

    private static (BuiltInType, NodeId, object) GetOpcUaTypeInfo(SimulatedDataType dataType)
    {
        return dataType switch
        {
            SimulatedDataType.UInt16 => (BuiltInType.UInt16, DataTypeIds.UInt16, (ushort)0),
            SimulatedDataType.Int16 => (BuiltInType.Int16, DataTypeIds.Int16, (short)0),
            SimulatedDataType.UInt32 => (BuiltInType.UInt32, DataTypeIds.UInt32, (uint)0),
            SimulatedDataType.Int32 => (BuiltInType.Int32, DataTypeIds.Int32, 0),
            SimulatedDataType.Float32 => (BuiltInType.Float, DataTypeIds.Float, 0.0f),
            SimulatedDataType.UInt64 => (BuiltInType.UInt64, DataTypeIds.UInt64, (ulong)0),
            SimulatedDataType.Int64 => (BuiltInType.Int64, DataTypeIds.Int64, (long)0),
            SimulatedDataType.Float64 => (BuiltInType.Double, DataTypeIds.Double, 0.0),
            _ => (BuiltInType.Float, DataTypeIds.Float, 0.0f)
        };
    }
}
