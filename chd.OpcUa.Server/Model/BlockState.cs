using chd.OpcUa.Base.Extensions;
using chd.OpcUa.Base.States;
using chd.OpcUa.Base.System.Attributes;
using chd.OpcUa.Contracts;
using chd.OpcUa.Server.UnderlyingSystem;
using chd.OpcUa.ServerWorker;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using chd.OpcUa.Server.Extensions;

namespace chd.OpcUa.Server.Model
{
    public class BlockState : BaseObjectState
    {
        private readonly UnderlyingSystemBlock _block;
        private readonly NodeManager _nodeManager;
        private int _monitoringCount;
        private int _eventCount;

        private Dictionary<string, ConditionState> _alarmStates = [];

        public BlockState(NodeManager nodeManager, NodeId nodeId, UnderlyingSystemBlock block) : base(null)
        {
            _nodeManager = nodeManager;
            _block = block;

            this.TypeDefinitionId = ObjectTypeIds.BaseObjectType;
            this.SymbolicName = block.Name;
            this.NodeId = nodeId;
            this.BrowseName = new QualifiedName(block.Name, nodeId.NamespaceIndex);
            this.DisplayName = new LocalizedText(block.Name);
            this.Description = new LocalizedText(block.Description);
            this.WriteMask = 0;
            this.UserWriteMask = 0;
            this.EventNotifier = block.GetEvents().Any() || block.GetAlarms().Any() ? EventNotifiers.SubscribeToEvents : EventNotifiers.None;

            this.RolePermissions = ModelUtils.GetUserRolePermissions(block, nodeManager.Server.NamespaceUris);
            this.UserRolePermissions = ModelUtils.GetUserRolePermissions(block, nodeManager.Server.NamespaceUris);

            foreach (var tag in block.GetTags())
            {
                var variable = CreateVariable(nodeManager.SystemContext, tag);
                AddChild(variable);
                variable.OnSimpleWriteValueAsync = OnWriteTagValueAsync;
                variable.OnSimpleReadValueAsync = OnReadTagValueAsync;
            }

            foreach (var method in block.GetMethods())
            {
                var methodNodeId = ModelUtils.ConstructIdForMethod(method.Identifier, nodeId.NamespaceIndex);
                var exec = new MethodExecutionState(nodeManager, methodNodeId, method, this);
                AddChild(exec);
            }
        }

        public void StartMonitoring(ServerSystemContext context)
        {
            if (_monitoringCount == 0)
            {
                _block?.StartMonitoring(OnTagsChanged);
            }
            _monitoringCount++;
        }

        /// <summary>
        /// Stop the monitoring the block.
        /// </summary>
        /// <param name="context">The context.</param>
        public bool StopMonitoring()
        {
            _monitoringCount--;

            if (_monitoringCount == 0)
            {
                _block?.StopMonitoring();
            }

            return _monitoringCount != 0;
        }

        public void SubscribeEvents()
        {
            if (_eventCount == 0)
            {
                _block.SubscribeEvents(OnEventTrigged);
                _block.SubscribeAlarms(OnAlarmTrigged);
            }
            _eventCount++;
        }

        public bool UnSubscribeEvents()
        {
            _eventCount--;

            if (_eventCount == 0)
            {
                _block.UnSubscribeEvents();
                _block.UnSubscribeAlarms();
            }
            return _eventCount != 0;
        }


        public override void ConditionRefresh(ISystemContext context, List<IFilterTarget> events, bool includeChildren)
        {
            foreach (var evt in this._block.GetEvents())
            {
                this.OnEventTrigged(evt, CancellationToken.None).AsTask().Wait();
            }
            foreach (var alarm in this._block.GetAlarms())
            {
                this.OnAlarmTrigged(alarm, CancellationToken.None).AsTask().Wait();
            }
            base.ConditionRefresh(context, events, includeChildren);
        }

        private ValueTask OnEventTrigged(UnderlyingSystemEvent? e, CancellationToken cancellationToken)
        {
            var baseEvent = this.CreateEvent(e, _nodeManager);
            if (baseEvent is null) { return ValueTask.CompletedTask; }
            baseEvent.ReceiveTime.Value = e.Time;
            return this.ReportEventAsync(_nodeManager.SystemContext, baseEvent, cancellationToken);
        }

        private ValueTask OnAlarmTrigged(UnderlyingSystemAlarm? e, CancellationToken cancellationToken)
        {
            if (!e.Id.HasValue)
            {
                e.Id = Guid.NewGuid();
            }
            var condition = this.CreateAlarm(e, _nodeManager);
            if (condition is null) { return ValueTask.CompletedTask; }

            condition.ReceiveTime.Value = e.Time;
            condition.OnEnableDisable = OnEnableDisableAlarm;
            condition.OnAcknowledge = OnAcknowledge;
            condition.OnAddComment = OnAddComment;
            condition.OnConfirm = OnConfirm;

            this._alarmStates[condition.EventId.Value.ToHexString()] = condition;

            var children = new List<BaseInstanceState>();
            this.GetChildren(_nodeManager.SystemContext, children);
            if (!children.Any(a => a is ConditionState cs && cs.EventId.Value.Equals(condition.EventId.Value)))
            {
                this.AddChild(condition);
            }

            return this.ReportEventAsync(_nodeManager.SystemContext, condition, cancellationToken);
        }

        private ServiceResult OnEnableDisableAlarm(
            ISystemContext context,
            ConditionState condition,
            bool enabling)
        {
            _block.EnableDiableAlarm(condition.SymbolicName, enabling);
            return ServiceResult.Good;
        }

        /// <summary>
        /// Called when the alarm has a comment added.
        /// </summary>
        private ServiceResult OnAddComment(ISystemContext context, ConditionState condition, ByteString eventId, LocalizedText comment)
        {
            if (!_alarmStates.TryGetValue(eventId.ToHexString(), out var alarm))
            {
                return StatusCodes.BadEventIdUnknown;
            }

            _block.CommentAlarm(alarm.SymbolicName, comment.Text, GetUserName(context));

            return ServiceResult.Good;
        }

        private ServiceResult OnAcknowledge(
            ISystemContext context,
            ConditionState condition,
            ByteString eventId,
            LocalizedText comment)
        {
            if (!_alarmStates.TryGetValue(eventId.ToHexString(), out var alarm))
            {
                return StatusCodes.BadEventIdUnknown;
            }

            _block.AcknowledgeAlarm(alarm.SymbolicName, comment.Text, GetUserName(context));

            return ServiceResult.Good;
        }

        /// <summary>
        /// Called when the alarm is confirmed.
        /// </summary>
        private ServiceResult OnConfirm(
            ISystemContext context,
            ConditionState condition,
            ByteString eventId,
            LocalizedText comment)
        {
            if (!_alarmStates.TryGetValue(eventId.ToHexString(), out var alarm))
            {
                return StatusCodes.BadEventIdUnknown;
            }

            _block.ConfirmAlarm(alarm.SymbolicName, comment.Text, GetUserName(context));

            return ServiceResult.Good;
        }



        private async ValueTask<AttributeSimpleReadResult> OnReadTagValueAsync(ISystemContext context, NodeState node,
            CancellationToken cancellationToken)
        {
            if (_block is null)
            {
                return new AttributeSimpleReadResult(ServiceResult.Bad, Variant.Null);
            }

            var (error, value) = await _block.ReadTagValueAsync(node.SymbolicName, cancellationToken);
            return new AttributeSimpleReadResult(error, value);


        }

        private async ValueTask<AttributeWriteResult> OnWriteTagValueAsync(
            ISystemContext context,
            NodeState node,
            Variant value,
            CancellationToken cancellationToken)
        {
            if (_block is null)
            {
                return new AttributeWriteResult(StatusCodes.BadNodeIdUnknown);
            }

            var error = await _block.WriteTagValueAsync(node.SymbolicName, value, cancellationToken);

            if (error != 0)
            {
                return new AttributeWriteResult(error);
            }

            return new AttributeWriteResult(ServiceResult.Good);
        }

        private void OnTagsChanged(object sender, UnderlyingSystemTag tag)
        {
            var variable = FindChildBySymbolicName(_nodeManager.SystemContext, tag.Name) as BaseVariableState;
            if (variable is not null)
            {
                UpdateVariable(tag, variable);
            }
            this.ClearChangeMasks(_nodeManager.SystemContext, true);
        }

        protected override void PopulateBrowser(ISystemContext context, NodeBrowser browser)
        {
            base.PopulateBrowser(context, browser);
            if (browser.BrowseDirection != BrowseDirection.Inverse)
            {
                var tags = new List<BaseInstanceState>();
                GetChildren(context, tags);

                for (int ii = 0; ii < tags.Count; ii++)
                {
                    browser.Add(tags[ii].ReferenceTypeId, false, tags[ii]);
                }
            }

            // check if the parent segments need to be returned.
            if (browser.IsRequired(ReferenceTypeIds.Organizes, true))
            {
                foreach (var segment in _nodeManager.UnderlyingSystemManager.FindSegmentsForBlock(_block.Identifier))
                {
                    browser.Add(ReferenceTypeIds.Organizes, true, ModelUtils.ConstructIdForSegment(segment.Identifier, this.NodeId.NamespaceIndex));
                }
            }
        }

        private string GetUserName(ISystemContext context)
        {
            return (context as ISessionSystemContext)?.UserIdentity?.DisplayName;
        }

        private BaseVariableState CreateVariable(ISystemContext context, UnderlyingSystemTag tag)
        {
            // create the variable type based on the tag type.
            BaseDataVariableState variable = null;

            if (tag.Labels is null
                && tag.Type.IsValueType || tag.Type == typeof(Guid) || tag.Type == typeof(string))
            {
                variable = new AnalogItemState(this);
            }
            else if (tag.Labels is not null && tag.Labels.Length <= 2)
            {
                variable = new TwoStateDiscreteState(this);
            }
            else if (tag.Labels.Length > 2 && tag.Type.IsEnum)
            {
                var node = new MultiStateDiscreteState(this);
                node.EnumStrings = PropertyState<ArrayOf<LocalizedText>>.With<VariantBuilder>(node);
                variable = node;
            }
            else
            {
                var node = new DataItemState(this);
                variable = node;
            }

            // set the symbolic name and reference types.
            variable.SymbolicName = tag.Name;
            variable.ReferenceTypeId = ReferenceTypeIds.HasComponent;

            // initialize the variable from the type model.
            variable.Create(
                context,
                NodeId.Null,
                new QualifiedName(tag.Name, this.BrowseName.NamespaceIndex),
                LocalizedText.Null,
                true);

            // update the variable values.
            UpdateVariable(tag, variable);

            variable.RolePermissions = ModelUtils.GetUserRolePermissions(tag, _nodeManager.Server.NamespaceUris);
            variable.UserRolePermissions = ModelUtils.GetUserRolePermissions(tag, _nodeManager.Server.NamespaceUris);

            return variable;
        }
        private void UpdateVariable(UnderlyingSystemTag tag, BaseVariableState variable)
        {
            variable.Description = new LocalizedText(tag.Description);
            variable.Value = tag.Value;
            variable.Timestamp = tag.Timestamp;
            variable.DataType = tag.Type.GetDataType();

            variable.ValueRank = ValueRanks.Scalar;
            variable.ArrayDimensions = ArrayOf<uint>.Empty;

            if (tag.IsWriteable)
            {
                variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
                variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
            }
            else
            {
                variable.AccessLevel = AccessLevels.CurrentRead;
                variable.UserAccessLevel = AccessLevels.CurrentRead;
            }

            variable.MinimumSamplingInterval = MinimumSamplingIntervals.Continuous;
            variable.Historizing = false;

            if (tag.Labels is not null && tag.Labels.Length == 2)
            {
                var node = variable as TwoStateDiscreteState;

                if (tag.Labels is not null && node.TrueState != null && node.FalseState != null)
                {
                    if (tag.Labels.Length >= 2)
                    {
                        node.TrueState.Value = new LocalizedText(tag.Labels[0]);
                        node.TrueState.Timestamp = tag.Block.Timestamp;
                        node.FalseState.Value = new LocalizedText(tag.Labels[1]);
                        node.FalseState.Timestamp = tag.Block.Timestamp;
                    }
                }
            }

            else if (tag.Labels is not null && tag.Labels.Length > 2 && tag.Type.IsEnum)
            {
                MultiStateDiscreteState node = variable as MultiStateDiscreteState;

                if (tag.Labels != null)
                {
                    LocalizedText[] strings = new LocalizedText[tag.Labels.Length];

                    for (int ii = 0; ii < tag.Labels.Length; ii++)
                    {
                        strings[ii] = new LocalizedText(tag.Labels[ii]);
                    }

                    node.EnumStrings.Value = strings.ToArrayOf();
                    node.EnumStrings.Timestamp = tag.Block.Timestamp;
                }
            }
        }
    }
}
