using chd.OpcUa.Base.Extensions;
using chd.OpcUa.Client.Extensions;
using chd.OpcUa.Contracts;
using chd.OpcUa.Contracts.Interfaces;
using chd.OpcUa.Contracts.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using Opc.Ua.Client.Subscriptions;
using Opc.Ua.Client.Subscriptions.MonitoredItems;
using Opc.Ua.Configuration;
using System.Diagnostics;
using System.Net;
using System.Security.Principal;
using System.Text.Unicode;
using System.Threading.Channels;
using System.Xml.Linq;
using chd.OpcUa.Base.States;
using static Opc.Ua.RelativePathFormatter;
using static System.Collections.Specialized.BitVector32;
using static System.Net.WebRequestMethods;
using MonitoredItemOptions = Opc.Ua.Client.Subscriptions.MonitoredItems.MonitoredItemOptions;
using SubscriptionOptions = Opc.Ua.Client.Subscriptions.SubscriptionOptions;

namespace chd.OpcUa.Client
{
    public class OpcUaClient(ILogger<OpcUaClient> logger,
        OpcUaClientOptions c,
        ITelemetryContext telemetryContext,
        NotificationHandler subscriptionNotificationHandler,
        IOptionsMonitor<OpcUaClientConnectionOptions> optionsMonitor,
        IOptionsMonitor<SubscriptionOptions> subscritptionsOptionsMonitor) : IOpcUAClient
    {
        private OpcUaClientConnectionOptions ConnectionOptions => optionsMonitor.CurrentValue;
        private ApplicationInstance? _instance;
        private ApplicationConfiguration? _configuration => _instance.ApplicationConfiguration;

        private List<NodeDto> _nodes = [];
        private List<NodeDto> _methods = [];

        private Dictionary<uint, EventFilter> _filtersByHandle = [];
        private Dictionary<uint, ConditionState> _conditionStates = [];

        private ISession _session;

        private ISubscription _monitoredItemSubscription;
        private ISubscription _eventsSubscription;

        private Channel<EventNotification> _eventChannel =
            Channel.CreateUnbounded<EventNotification>(new UnboundedChannelOptions
            { SingleReader = true, SingleWriter = false });

        private Task _channelConsumer;
        private ComplexTypeSystem _typeSystem;

        public bool IsConnected => _session is not null && _session.Connected;

        public event AsyncEventHandler<MonitoredItemEventArgs> MonitoredItemNotification;
        public event AsyncEventHandler<AlarmEventArgs> AlarmNotification;
        public event AsyncEventHandler<SimpleEventArgs> EventNotification;


        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var timeout = (int?)ConnectionOptions.Timeout?.TotalMilliseconds ?? 60000;
            await InitializeApplicationInstance(cancellationToken);

            var identity = GetIdentity();

            var endpoint = await GetEndpointAsync(identity, cancellationToken);

            await CreateSessionAsync(endpoint, timeout, identity, cancellationToken);
            if (ConnectionOptions.StartNodes?.Any() ?? false)
            {
                foreach (var startNode in ConnectionOptions.StartNodes)
                {
                    await BrowseNodeAsync(NodeId.Parse(null, startNode), cancellationToken);
                }
            }
            else
            {
                await BrowseNodeAsync(ObjectIds.ObjectsFolder, cancellationToken);
            }
        }

        public Task<T> ReadAsync<T>(string node, CancellationToken cancellationToken)
            => ExecuteForNode(node, (n) => _session.ReadValueAsync<T>(n, cancellationToken));

        public Task<bool> WriteAsync<T>(string node, T value, CancellationToken cancellationToken)
            => ExecuteForNode(node, async (n) =>
            {
                var nodesToRead = new List<ReadValueId> {
                    new ReadValueId { NodeId = n, AttributeId = Attributes.Value },
                };

                var results = await _session.ReadAsync(null, 0, TimestampsToReturn.Neither, nodesToRead, cancellationToken);
                var dataValue = results.Results[0];
                var writeValue = new WriteValue()
                {
                    NodeId = n,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(dataValue.ChangeType(value), StatusCodes.Good, DateTime.MinValue, DateTime.MinValue)
                };
                var res = await _session.WriteAsync(null, new[] { writeValue }, cancellationToken);
                return res.Results.ToList().All(a => StatusCode.IsGood(a));
            });

        public Task<bool> MonitorItem(string node, int sampingInteral = 500, CancellationToken cancellationToken = default)
            => ExecuteForNode<bool>(node, n =>
            {
                var options = new MonitoredItemOptions
                {
                    StartNodeId = n,
                    AttributeId = Attributes.Value,
                    MonitoringMode = MonitoringMode.Reporting,
                    SamplingInterval = TimeSpan.FromMilliseconds(sampingInteral),
                    QueueSize = 0,
                    DiscardOldest = true,
                };
                CreateSubscription();

                if (_monitoredItemSubscription.MonitoredItems.TryAdd(node, new Opc.Ua.OptionsMonitor<MonitoredItemOptions>(options),
                        out IMonitoredItem monitoredItem))
                {
                    return Task.FromResult(StatusCode.IsGood(monitoredItem.Error.StatusCode));
                }

                return Task.FromResult(false);
            });

        public Task<bool> AttachToEventsAsync(string node, CancellationToken cancellationToken = default)
            => ExecuteForNode<bool>(node, async n =>
            {
                CreateEventsSubscription(cancellationToken);

                var customEventTypeId = new NodeId("ns=2;s=5:CustomEventType");

                var filter = new EventFilter();

                // Standardfelder von BaseEventType
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.EventId));
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.EventType));
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.SourceNode));
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.SourceName));
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.Time));
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.Message));
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, new QualifiedName(BrowseNames.Severity));

                // Custom-Feld "Value" vom CustomEventType via BrowsePath
                filter.AddSelectClause(customEventTypeId, new QualifiedName(nameof(CustomEventState<>.Value), 2));


                var element1 = filter.WhereClause.Push(FilterOperator.OfType, Opc.Ua.ObjectTypeIds.AlarmConditionType);
                filter.WhereClause.Push(FilterOperator.Not, Variant.From(new ExtensionObject(element1)));

                var options = new MonitoredItemOptions
                {
                    StartNodeId = n,
                    AttributeId = Attributes.EventNotifier,
                    SamplingInterval = TimeSpan.Zero,
                    MonitoringMode = MonitoringMode.Reporting,
                    QueueSize = 1000,
                    DiscardOldest = true,
                    Filter = filter,
                };

                if (_eventsSubscription.MonitoredItems.TryAdd(node, new Opc.Ua.OptionsMonitor<MonitoredItemOptions>(options), out IMonitoredItem monitoredItem))
                {
                    _filtersByHandle[monitoredItem.ClientHandle] = (EventFilter)options.Filter;
                    return true;
                }
                return false;
            }, NodeClass.Object);

        public async Task AcknowledgeAsync(uint handle, ReadOnlyMemory<byte> eventId, string comment, CancellationToken cancellationToken)
        {
            if (!_conditionStates.TryGetValue(handle, out var condition))
            {
                return;
            }

            var client = new AcknowledgeableConditionTypeClient(_session, condition.NodeId, telemetryContext);
            await client.AcknowledgeAsync(new ByteString(eventId),
                new LocalizedText(comment), cancellationToken);
        }

        public async Task AddCommentAsync(uint handle, ReadOnlyMemory<byte> eventId, string comment, CancellationToken cancellationToken)
        {
            if (!_conditionStates.TryGetValue(handle, out var condition))
            {
                return;
            }

            var client = new AcknowledgeableConditionTypeClient(_session, condition.NodeId, telemetryContext);
            await client.AddCommentAsync(new ByteString(eventId),
                new LocalizedText(comment), cancellationToken);
        }

        public async Task ConfirmAsync(uint handle, ReadOnlyMemory<byte> eventId, string comment, CancellationToken cancellationToken)
        {
            if (!_conditionStates.TryGetValue(handle, out var condition))
            {
                return;
            }

            var client = new AcknowledgeableConditionTypeClient(_session, condition.NodeId, telemetryContext);
            await client.ConfirmAsync(new ByteString(eventId),
                new LocalizedText(comment), cancellationToken);
        }

        public bool RemoveMonitorItem(string node)
            => _monitoredItemSubscription.MonitoredItems.TryGetMonitoredItemByName(node, out var item)
               && _monitoredItemSubscription.MonitoredItems.TryRemove(item.ClientHandle);

        public async Task<IEnumerable<object>> CallMethod(string method, CancellationToken cancellationToken = default, params object[] inputs)
        {
            var response = await CallMethod(method, inputs, cancellationToken);
            return response.ToList().Select(s => s.GetValue());
        }

        public async Task<TOutput> CallMethod<TInput, TOutput>(string method, TInput input, CancellationToken cancellationToken = default)
        where TInput : struct
        where TOutput : struct
        {
            var response = await CallMethod(method, input.ToInputArray(), cancellationToken);
            return response.ToList().Select(s => s.GetValue()).ToOuputData<TOutput>();
        }

        private Task<ArrayOf<Variant>> CallMethod(string method, object[] inputs, CancellationToken cancellationToken)
        {
            if (this._methods.All(a => a.Identifier != method))
            {
                throw new Exception($"Konnte die Methode {method} nicht finden");
            }

            var dto = this._methods.FirstOrDefault(x => x.Identifier == method);
            return this._session.CallAsync(dto.ParentNodeId, dto.Node.InnerNodeId, cancellationToken, inputs.Select(s => new Variant(s)).ToArray());
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_monitoredItemSubscription is not null)
            {
                await _monitoredItemSubscription.DisposeAsync();
            }

            if (_session is null)
            {
                return;
            }
            _session.KeepAlive -= _session_KeepAlive;
            if (_session is ManagedSession managedSession)
            {
                managedSession.ConnectionStateChanged -= OnConnectionStateChanged;
            }

            _ = await _session.CloseAsync(cancellationToken);
            _nodes.Clear();
            _methods.Clear();
        }


        private Task<T> ExecuteForNode<T>(string node, Func<NodeId, Task<T>> func, NodeClass nodeClass = NodeClass.Variable)
        {
            if (_nodes.Any(a => a.Identifier == node && a.Description.NodeClass == nodeClass))
            {
                var cachedNode = _nodes.FirstOrDefault(a => a.Identifier == node);
                return func(cachedNode.Node.InnerNodeId);
            }
            throw new Exception($"Konten {node} nicht gefunden!");
        }

        private void CreateSubscription()
        {
            if (_monitoredItemSubscription is null && _session.TryGetSubscriptionManager(out var manager))
            {
                subscriptionNotificationHandler.DataChangeCallback = NotifyMonitoredItemAsync;
                _monitoredItemSubscription = manager.Add(subscriptionNotificationHandler, subscritptionsOptionsMonitor);
            }
        }

        private void CreateEventsSubscription(CancellationToken cancellationToken)
        {
            if (_eventsSubscription is null && _session.TryGetSubscriptionManager(out var manager))
            {
                subscriptionNotificationHandler.EventCallback = RaiseEventsAsync;
                _eventsSubscription = manager.Add(subscriptionNotificationHandler, subscritptionsOptionsMonitor);
            }

            if (_channelConsumer is null)
            {
                _channelConsumer = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var notification in _eventChannel.Reader.ReadAllAsync(cancellationToken)
                                           .ConfigureAwait(false))
                        {
                            var args = await _session.ProcessEventNotificationAsync(_filtersByHandle, notification,
                                cancellationToken);
                            if (args is not null
                                && EventNotification is not null)
                            {
                                await this.EventNotification.Invoke(this, args);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, ex.Message);
                    }
                }, cancellationToken);
            }
        }

        private async ValueTask NotifyMonitoredItemAsync(ISubscription subscription, uint seqNr, DateTime publishTime, DataValueChange[] changes)
        {
            if (MonitoredItemNotification is not null)
            {
                foreach (var change in changes)
                {
                    await MonitoredItemNotification.Invoke(this,
                        new MonitoredItemEventArgs(change.MonitoredItem.Name, change.Value.GetValue(), publishTime));
                }
            }
        }

        private async ValueTask RaiseEventsAsync(ISubscription subscription, uint seqNr, DateTime publishTime, EventNotification[] events)
        {
            foreach (var eventAlarm in events)
            {
                await _eventChannel.Writer.WriteAsync(eventAlarm);
            }
        }

        private async Task BrowseNodeAsync(NodeId? parentId, CancellationToken cancellationToken)
        {
            parentId ??= ObjectIds.ObjectsFolder;
            var nodesToBrowse = new List<BrowseDescription> {
                    new BrowseDescription {
                        NodeId = parentId.Value,
                        BrowseDirection = BrowseDirection.Forward,
                        ReferenceTypeId = ReferenceTypeIds.Aggregates,
                        IncludeSubtypes = true,
                        NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method),
                        ResultMask = (uint)BrowseResultMask.All,
                    },
                    new BrowseDescription {
                        NodeId = parentId.Value,
                        BrowseDirection = BrowseDirection.Forward,
                        ReferenceTypeId = ReferenceTypeIds.Organizes,
                        IncludeSubtypes = true,
                        NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method),
                        ResultMask = (uint)BrowseResultMask.All,
                    },
                };
            foreach (var child in await _session.BrowseAsync(nodesToBrowse, cancellationToken))
            {
                switch (child.NodeClass)
                {
                    case NodeClass.Method:
                        _methods.Add(new(child, parentId.Value));
                        break;
                    default:
                        _nodes.Add(new(child, parentId.Value));
                        await BrowseNodeAsync(child.NodeId.InnerNodeId, cancellationToken);
                        break;
                }
            }
        }

        private async Task InitializeApplicationInstance(CancellationToken cancellationToken)
        {
            if (_instance is null)
            {
                _instance = new ApplicationInstance(telemetryContext)
                {
                    ApplicationType = ApplicationType.Client,
                    ApplicationName = ConnectionOptions.Name,
                };

                _instance.ApplicationConfiguration = c.Configuration;

                if (await _instance.CheckApplicationInstanceCertificatesAsync(false, ct: cancellationToken))
                {
                    _instance.CertificateManager.AutoAcceptUntrustedCertificates = true;
                }
            }
        }

        private IUserIdentity? GetIdentity()
        {
            IUserIdentity identity = null;
            if (!string.IsNullOrWhiteSpace(ConnectionOptions.Username))
            {
                var pwBytes = new Span<byte>();
                _ = Utf8.FromUtf16(ConnectionOptions.Password, pwBytes, out _, out _);
                identity = new UserIdentity(ConnectionOptions.Username, pwBytes);
            }

            return identity;
        }

        private async Task CreateSessionAsync(ConfiguredEndpoint endpoint, int timeout, IUserIdentity identity, CancellationToken cancellationToken)
        {
            _session = await new ManagedSessionFactory(telemetryContext).CreateAsync(
                _configuration,
                endpoint,
                false,
                true,
                !string.IsNullOrWhiteSpace(ConnectionOptions.Name) ? ConnectionOptions.Name : nameof(OpcUaClient),
                (uint)timeout,
                identity ?? new UserIdentity(), new string[] { },
                cancellationToken);

            _session.KeepAlive += _session_KeepAlive;

            if (_session is ManagedSession managedSession)
            {
                managedSession.ConnectionStateChanged += OnConnectionStateChanged;
            }

            try
            {
                _typeSystem = ComplexTypeSystemClientExtensions.Create(_session, telemetryContext);

                _ = await _typeSystem.LoadAsync(ct: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger?.LogWarning(e, "Failed to load complex type system.");
            }
        }

        private async Task<ConfiguredEndpoint> GetEndpointAsync(IUserIdentity identity, CancellationToken cancellationToken)
        {
            var endpointConfiguration = EndpointConfiguration.Create(_configuration);

            var discoveryClient = await DiscoveryClient.CreateAsync(_configuration, new Uri(this.ConnectionOptions.EndpointUrl),
                endpointConfiguration, DiagnosticsMasks.All, cancellationToken);

            EndpointDescription? selectedEndpoint = null;

            foreach (var ep in (await discoveryClient.GetEndpointsAsync(new string[] { }, cancellationToken)).ToList())
            {
                if (identity is not null
                    && ConnectionOptions.UseCertificate
                    && ep.SecurityMode is MessageSecurityMode.SignAndEncrypt)
                {
                    selectedEndpoint = ep;
                    break;
                }
                if (identity is not null
                    && !ConnectionOptions.UseCertificate
                    && ep.SecurityMode is MessageSecurityMode.Sign)
                {
                    selectedEndpoint = ep;
                    break;
                }
                if (identity is null
                    && !ConnectionOptions.UseCertificate
                    && ep.SecurityMode is MessageSecurityMode.None)
                {
                    selectedEndpoint = ep;
                    break;
                }
            }

            if (selectedEndpoint is null)
            {
                throw new Exception($"Konnte keinen validen Endpoint auf {ConnectionOptions.EndpointUrl} finden!");
            }

            return new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
        }

        private void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            if (_session is null || (sender is ISession sessionOfEvent && !ReferenceEquals(sessionOfEvent, _session)))
            {
                return;
            }

            switch (e.NewState)
            {
                case ConnectionState.Reconnecting:
                case ConnectionState.Failover:
                    {
                        logger?.LogWarning($"Reconnecting (attempt {0})", e.ReconnectAttempt);
                        break;
                    }

                case ConnectionState.Connected:
                    {
                        logger?.LogDebug($"Session Connected {_session.Endpoint.EndpointUrl}");
                        break;
                    }

                case ConnectionState.Disconnected:
                    {
                        logger?.LogError($"Session Disconnected {e.Error}");
                        break;
                    }
            }
        }

        private void _session_KeepAlive(ISession session, KeepAliveEventArgs e)
        {
            logger?.LogTrace($"Session changed to {e.CurrentState}");
        }

        public async ValueTask DisposeAsync()
        {
            _typeSystem?.Dispose();
            subscriptionNotificationHandler.DataChangeCallback = null;
            subscriptionNotificationHandler.EventCallback = null;
            subscriptionNotificationHandler.KeepAliveCallback = null;
            subscriptionNotificationHandler.StateChangedCallback = null;
        }
    }
}
