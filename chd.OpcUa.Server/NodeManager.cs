using chd.OpcUa.Contracts.Interfaces;
using chd.OpcUa.Server.Model;
using chd.OpcUa.Server.UnderlyingSystem;
using Opc.Ua;
using Opc.Ua.Server;
using Opc.Ua.Server.Fluent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using chd.OpcUa.Base.States;

namespace chd.OpcUa.ServerWorker
{
    public class NodeManager : FluentNodeManagerBase
    {
        private readonly IUnderlyingSystemManager<UnderlyingSystemSegment, UnderlyingSystemBlock, UnderlyingSystemMethod> _underlyingSystemManager;
        private NodeIdDictionary<BlockState> _blocks = new();
        private NodeIdDictionary<BlockState> _eventBlocks = new();
        private NodeIdDictionary<MethodExecutionState> _methods = new();

        public IUnderlyingSystemManager<UnderlyingSystemSegment, UnderlyingSystemBlock, UnderlyingSystemMethod> UnderlyingSystemManager => this._underlyingSystemManager;

        public NodeManager(IServerInternal server, ApplicationConfiguration configuration, IUnderlyingSystemManager<UnderlyingSystemSegment, UnderlyingSystemBlock, UnderlyingSystemMethod> underlyingSystemManager, params string[] namespaces)
            : base(server, configuration, server.Telemetry.CreateLogger<NodeManager>(), namespaces)
        {
            _underlyingSystemManager = underlyingSystemManager;
            this.AliasRoot = "CHD";
        }

        public override NodeId New(ISystemContext context, NodeState node)
            => ModelUtils.ConstructIdForComponent(node, NamespaceIndex);

        public override async ValueTask CreateAddressSpaceAsync(
            IDictionary<NodeId, IList<IReference>> externalReferences,
            CancellationToken cancellationToken = default)
        {
            await base.CreateAddressSpaceAsync(externalReferences, cancellationToken).ConfigureAwait(false);


            var simpleEventTypeId = ModelUtils.ConstructIdForEventType<SimpleValueCustomEventState>(NamespaceIndex);

            MasterNodeManager.CreateExternalReference(externalReferences, ObjectTypeIds.BaseEventType,
                ReferenceTypeIds.HasSubtype,
                false,
                simpleEventTypeId);

            var objectEventTypeId = ModelUtils.ConstructIdForEventType<ObjectValueCustomEventState>(NamespaceIndex);
            var complexEventTypeId = ModelUtils.ConstructIdForEventType<ComplexValueCustomEventState>(NamespaceIndex);

            MasterNodeManager.CreateExternalReference(
                externalReferences,
                ObjectTypeIds.BaseEventType,
                ReferenceTypeIds.HasSubtype,
                false,
                objectEventTypeId);
            MasterNodeManager.CreateExternalReference(
                externalReferences,
                ObjectTypeIds.BaseEventType,
                ReferenceTypeIds.HasSubtype,
                false,
                complexEventTypeId);


            foreach (var segment in await _underlyingSystemManager.GetMainSegmentsAsync(cancellationToken))
            {
                IList<IReference> references = null;
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                var segmentId = ModelUtils.ConstructIdForSegment(segment.Identifier, NamespaceIndex);
                var node = new NodeStateReference(ReferenceTypeIds.Organizes, false, segmentId);
                references.Add(node);
            }
            var builder = CreateFluentBuilder(NamespaceIndex);

            builder
                .ResolveNodes(IsSegmentOrBlockId, ResolveSegmentOrBlockAsync)
                .OnMonitoredItemCreated(OnBlockMonitoredItemCreated)
                .OnMonitoredItemDeleted(OnBlockMonitoredItemDeletedAsync);

            builder.ResolveNodes(IsMethodId, ResolveMethodAsync);

            builder.ResolveNodes(IsEventTypeId, ResolveEventAsync);

            await RegisterAuthoredNodesAsync(builder, cancellationToken).ConfigureAwait(false);
            await CompleteConfigureAsync(externalReferences, cancellationToken).ConfigureAwait(false);
            await SealConfigurationAsync(builder, cancellationToken).ConfigureAwait(false);


        }

        public override ValueTask<ServiceResult> ConditionRefreshAsync(OperationContext context, IList<IEventMonitoredItem> monitoredItems,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return base.ConditionRefreshAsync(context, monitoredItems, cancellationToken);
        }

        protected override ValueTask<ServiceResult> SubscribeToEventsAsync(ServerSystemContext context, NodeState source, IEventMonitoredItem monitoredItem,
            bool unsubscribe, CancellationToken cancellationToken = new CancellationToken())
        {
            if (source is BlockState blockState)
            {
                if (!unsubscribe)
                {
                    blockState.SubscribeEvents();
                    _eventBlocks[blockState.NodeId] = blockState;
                }
                else
                {
                    if (!blockState.UnSubscribeEvents())
                    {
                        _eventBlocks.TryRemove(blockState.NodeId, out _);
                    }
                }
            }
            return base.SubscribeToEventsAsync(context, source, monitoredItem, unsubscribe, cancellationToken);
        }

        private static bool IsSegmentOrBlockId(NodeId nodeId) => nodeId.IdType is IdType.String && ParsedNodeId.Parse(nodeId).RootType <= ModelUtils.Block;
        private static bool IsMethodId(NodeId nodeId) => nodeId.IdType is IdType.String && ParsedNodeId.Parse(nodeId).RootType > ModelUtils.Block && ParsedNodeId.Parse(nodeId).RootType <= ModelUtils.OutputArgument;

        private static bool IsEventTypeId(NodeId nodeId) => nodeId.IdType switch
        {
            IdType.String => ParsedNodeId.Parse(nodeId).RootType > ModelUtils.OutputArgument && ParsedNodeId.Parse(nodeId).RootType <= ModelUtils.EventType,
            _ => false
        };

        private async ValueTask<NodeState> ResolveSegmentOrBlockAsync(ISystemContext context, NodeId nodeId, CancellationToken cancellationToken)
        {
            var parsedNodeId = ParsedNodeId.Parse(nodeId);

            if (parsedNodeId is null)
            {
                return default;
            }

            NodeState root = null;

            if (parsedNodeId.RootType == ModelUtils.Segment)
            {
                var segment = await _underlyingSystemManager.FindSegmentByIdentifier(parsedNodeId.RootId, cancellationToken);

                if (segment is null)
                {
                    return default;
                }

                var rootId = ModelUtils.ConstructIdForSegment(segment.Identifier, NamespaceIndex);

                var blocks = _underlyingSystemManager.FindBlocksForSegment(segment);
                root = new SegmentState(rootId, segment, blocks);

                if (blocks?.Any(a => a.GetEvents().Any()) ?? false)
                {
                    foreach (var block in blocks.Where(x => x.GetEvents().Any()))
                    {
                        var blockState = new BlockState(this,
                            ModelUtils.ConstructIdForBlock(block.Identifier, NamespaceIndex), block);
                        root.AddNotifier(context, ReferenceTypeIds.HasEventSource, false, blockState);
                    }
                }
            }

            else if (parsedNodeId.RootType == ModelUtils.Block)
            {
                var block = await _underlyingSystemManager.FindBlockByIdentifier(parsedNodeId.RootId, cancellationToken);
                if (block is null)
                {
                    return default;
                }

                var segments = _underlyingSystemManager.FindSegmentsForBlock(block.Identifier);

                var rootId = ModelUtils.ConstructIdForBlock(block.Identifier, NamespaceIndex);

                if (_blocks.TryGetValue(rootId, out BlockState node))
                {
                    root = node;
                }
                else if (_eventBlocks.TryGetValue(rootId, out var eventNode))
                {
                    root = eventNode;
                }
                else
                {
                    root = new BlockState(this, rootId, block);
                    if (block.GetEvents().Any())
                    {
                        foreach (var segment in segments)
                        {
                            var segmentState =
                                new SegmentState(ModelUtils.ConstructIdForSegment(segment.Identifier, NamespaceIndex),
                                    segment, [block]);
                            root.AddNotifier(context, ReferenceTypeIds.HasEventSource, true, segmentState);
                        }
                    }
                }
            }
            else
            {
                return default;
            }

            if (string.IsNullOrEmpty(parsedNodeId.ComponentPath))
            {
                return root;
            }
            var child =  root.FindChildBySymbolicName(context, parsedNodeId.ComponentPath);
            return child;
        }

        private async ValueTask<NodeState> ResolveMethodAsync(ISystemContext context, NodeId nodeId, CancellationToken cancellationToken)
        {
            var parsedNodeId = ParsedNodeId.Parse(nodeId);

            if (parsedNodeId is null)
            {
                return default;
            }

            NodeState root = null;

            if (parsedNodeId.RootType == ModelUtils.Method)
            {
                var method =
                    await _underlyingSystemManager.FindMethodByIdentifier(parsedNodeId.RootId, cancellationToken);
                if (method is null)
                {
                    return default;
                }
                var rootId = ModelUtils.ConstructIdForMethod(method.Identifier, NamespaceIndex);
                if (_methods.TryGetValue(rootId, out var methodState))
                {
                    root = methodState;
                }
                else
                {
                    root = new MethodExecutionState(this, rootId, method, null);
                    this._methods.Add(rootId, (MethodExecutionState)root);
                }
            }
            else if (parsedNodeId.RootType == ModelUtils.InputArgument)
            {
                var method =
                    await _underlyingSystemManager.FindMethodByIdentifier(parsedNodeId.RootId.Replace(":Input", ""), cancellationToken);
                if (method is null)
                {
                    return default;
                }
                var rootId = ModelUtils.ConstructIdForMethod(method.Identifier, NamespaceIndex);

                if (_methods.TryGetValue(rootId, out var methodState))
                {
                    root = methodState.InputArguments;
                }
            }
            else if (parsedNodeId.RootType == ModelUtils.OutputArgument)
            {
                var method =
                    await _underlyingSystemManager.FindMethodByIdentifier(parsedNodeId.RootId.Replace(":Output", ""), cancellationToken);
                if (method is null)
                {
                    return default;
                }
                var rootId = ModelUtils.ConstructIdForMethod(method.Identifier, NamespaceIndex);
                if (_methods.TryGetValue(rootId, out var methodState))
                {
                    root = methodState.OutputArguments;
                }
            }
            else
            {
                return default;
            }

            if (string.IsNullOrEmpty(parsedNodeId.ComponentPath))
            {
                return root;
            }
            return root.FindChildBySymbolicName(context, parsedNodeId.ComponentPath);
        }

        private async ValueTask<NodeState> ResolveEventAsync(ISystemContext context, NodeId nodeId, CancellationToken cancellationToken)
        {
            var parsedNodeId = ParsedNodeId.Parse(nodeId);

            if (parsedNodeId is null)
            {
                return default;
            }

            NodeState root = null;
            if (parsedNodeId.RootType == ModelUtils.EventType &&
                parsedNodeId.RootId == nameof(SimpleValueCustomEventState).Replace("State", "Type"))
            {
                root = SimpleValueCustomEventState.CreateEventType<SimpleValueCustomEventState>(ModelUtils.ConstructIdForEventType<SimpleValueCustomEventState>(NamespaceIndex), state => ModelUtils.ConstructIdForComponent(state, NamespaceIndex), NamespaceIndex);
            }
            else if (parsedNodeId.RootType == ModelUtils.EventType &&
                parsedNodeId.RootId == nameof(ObjectValueCustomEventState).Replace("State", "Type"))
            {
                root = ObjectValueCustomEventState.CreateEventType<ObjectValueCustomEventState>(ModelUtils.ConstructIdForEventType<ObjectValueCustomEventState>(NamespaceIndex), state => ModelUtils.ConstructIdForComponent(state, NamespaceIndex), NamespaceIndex);
            }
            else if (parsedNodeId.RootType == ModelUtils.EventType &&
                parsedNodeId.RootId == nameof(ComplexValueCustomEventState).Replace("State", "Type"))
            {
                root = ComplexValueCustomEventState.CreateEventType<ComplexValueCustomEventState>(ModelUtils.ConstructIdForEventType<ComplexValueCustomEventState>(NamespaceIndex), state => ModelUtils.ConstructIdForComponent(state, NamespaceIndex), NamespaceIndex);
            }
            else
            {
                root = default;
            }


            if (string.IsNullOrEmpty(parsedNodeId.ComponentPath))
            {
                return root;
            }

            return root.FindChildBySymbolicName(context, parsedNodeId.ComponentPath);
        }

        private void OnBlockMonitoredItemCreated(
            ISystemContext context,
            NodeState source,
            ISampledDataChangeMonitoredItem monitoredItem)
        {
            if (source.GetHierarchyRoot() is BlockState block)
            {
                block.StartMonitoring((ServerSystemContext)context);
                _blocks[block.NodeId] = block;
            }
        }

        private ValueTask OnBlockMonitoredItemDeletedAsync(
            ISystemContext context,
            NodeState source,
            ISampledDataChangeMonitoredItem monitoredItem,
            CancellationToken cancellationToken)
        {
            if (source.GetHierarchyRoot() is BlockState block &&
                !block.StopMonitoring())
            {
                _blocks.TryRemove(block.NodeId, out _);
            }

            return default;
        }
    }
}
