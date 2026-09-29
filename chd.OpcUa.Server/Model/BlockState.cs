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
            var baseEvent = CreateEvent(e);
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
            var condition = CreateCondition(e);
            if (condition is null) { return ValueTask.CompletedTask; }
            condition.ReceiveTime.Value = e.Time;



            return this.ReportEventAsync(_nodeManager.SystemContext, condition, cancellationToken);
        }



        private BaseEventState CreateEvent(UnderlyingSystemEvent e)
            => e.Type switch
            {
                var x when !x.Equals(typeof(void)) && x.IsValueType || x.Equals(typeof(string)) => CreateSimpleValueEvent(e),
                var x when !x.Equals(typeof(void)) && !x.IsValueType && x.IsClass => CreateObjectValueEvent(e),
                _ => CreateBaseEvent(e)
            };


        private AlarmConditionState CreateCondition(UnderlyingSystemAlarm e)
        {
            var condition = new AlarmConditionState(this);
            condition.Initialize(_nodeManager.SystemContext, this, e.Severity, LocalizedText.From(e.Message));



            condition.Acknowledge = new AddCommentMethodState(condition);

            condition.EnabledState = new TwoStateVariableState(condition);
            condition.EnabledState.TransitionTime = PropertyState<DateTimeUtc>.With<VariantBuilder>(condition.EnabledState);
            condition.EnabledState.EffectiveDisplayName = PropertyState<LocalizedText>.With<VariantBuilder>(condition.EnabledState);
            condition.EnabledState.Create(_nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.EnabledState), LocalizedText.Null, false);
            

            // same procedure add optional components to the ActiveState component.
            condition.ActiveState = new TwoStateVariableState(condition);
            condition.ActiveState.TransitionTime = PropertyState<DateTimeUtc>.With<VariantBuilder>(condition.ActiveState);
            condition.ActiveState.EffectiveDisplayName = PropertyState<LocalizedText>.With<VariantBuilder>(condition.ActiveState);
            condition.ActiveState.Create(_nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.ActiveState), LocalizedText.Null, false);
           

            // same procedure add optional components to the ActiveState component.
            condition.ConfirmedState = new TwoStateVariableState(condition);
            condition.ConfirmedState.TransitionTime = PropertyState<DateTimeUtc>.With<VariantBuilder>(condition.ConfirmedState);
            condition.ConfirmedState.EffectiveDisplayName = PropertyState<LocalizedText>.With<VariantBuilder>(condition.ConfirmedState);
            condition.ConfirmedState.Create(_nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.ConfirmedState), LocalizedText.Null, false);
            condition.Confirm = new AddCommentMethodState(condition);

            condition.Comment = ConditionVariableState<LocalizedText>.With<VariantBuilder>(condition);
            condition.Comment.Create(_nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.Comment), LocalizedText.Null, false);
            
            condition.AddComment = new AddCommentMethodState(condition);

            condition.SymbolicName = e.Name;
            condition.SupportsFilteredRetain = PropertyState<bool>.With<VariantBuilder>(condition, true);

            condition.Create(_nodeManager.SystemContext, NodeId.Null, new QualifiedName(e.Name, _nodeManager.NamespaceIndex), LocalizedText.From(e.Name), true);

            condition.EnabledState.Id.Value = e.Enabled;
            condition.EnabledState.Value = LocalizedText.From("Enabled");
            condition.ActiveState.Id.Value = e.Active;
            condition.AckedState.Id.Value = e.Acknowledged;
            condition.ConfirmedState.Id.Value = e.Confirmed;

            condition.Comment.Value = LocalizedText.From(e.Comment);

            condition.EventId.Value = e.Id.Value.ToByteArray().ToByteString();
            condition.EventType.Value = condition.TypeDefinitionId;
            condition.ConditionName = PropertyState<string>.With<VariantBuilder>(condition);
            condition.ConditionName.Value = condition.SymbolicName;
            condition.Time.Value = DateTime.UtcNow;
            condition.ReceiveTime.Value = condition.Time.Value;

            condition.Retain.Value = true;

            // set up method handlers.
            condition.OnEnableDisable = OnEnableDisableAlarm;
            condition.OnAcknowledge = OnAcknowledge;
            condition.OnAddComment = OnAddComment;
            condition.OnConfirm = OnConfirm;

            this._alarmStates[condition.EventId.Value.ToHexString()] = condition;

            this.AddChild(condition);

            return condition;
        }
        private BaseEventState CreateBaseEvent(UnderlyingSystemEvent e)
        {
            var evt = new BaseEventState(null);
            evt.Initialize(_nodeManager.SystemContext, this, e.Severity, LocalizedText.From(e.Message));
            return evt;
        }

        private SimpleValueCustomEventState CreateSimpleValueEvent(UnderlyingSystemEvent e)
        {
            var evt = new SimpleValueCustomEventState(
                ModelUtils.ConstructIdForEventType<SimpleValueCustomEventState>(_nodeManager.NamespaceIndex),
                state => ModelUtils.ConstructIdForComponent(state, _nodeManager.NamespaceIndex), this,
                _nodeManager.NamespaceIndex);
            evt.Initialize(_nodeManager.SystemContext, this, e.Severity, LocalizedText.From(e.Message));

            evt.Value.Value = e.Value?.ConvertToVariant();
            return evt;
        }
        private ComplexValueCustomEventState CreateComplexValueEvent(UnderlyingSystemEvent e)
        {
            var evt = new ComplexValueCustomEventState(
                ModelUtils.ConstructIdForEventType<ComplexValueCustomEventState>(_nodeManager.NamespaceIndex),
                state => ModelUtils.ConstructIdForComponent(state, _nodeManager.NamespaceIndex), this,
                _nodeManager.NamespaceIndex);
            evt.Initialize(_nodeManager.SystemContext, this, e.Severity, LocalizedText.From(e.Message));

            evt.Value.Value = e.Value;
            return evt;
        }
        private ObjectValueCustomEventState CreateObjectValueEvent(UnderlyingSystemEvent e)
        {
            var evt = new ObjectValueCustomEventState(
                ModelUtils.ConstructIdForEventType<ObjectValueCustomEventState>(_nodeManager.NamespaceIndex),
                state => ModelUtils.ConstructIdForComponent(state, _nodeManager.NamespaceIndex), this,
                _nodeManager.NamespaceIndex);
            evt.Initialize(_nodeManager.SystemContext, this, e.Severity, LocalizedText.From(e.Message));

            var props = e.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            evt.Value.Value = props.Select(s => s.GetValue(e.Value)).ToArray();
            return evt;
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
