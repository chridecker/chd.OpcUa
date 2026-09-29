using chd.OpcUa.Base.States;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Base.States
{
    public abstract class CustomEventState<TValue> : BaseEventState
    {
        private readonly NodeId _nodeId;

        public PropertyState<TValue> Value { get; set; }

        public CustomEventState(NodeState? parent, ushort namespaceIndex) : base(parent)
        {
            Value = PropertyState<TValue>.With<EventValueBuilder<TValue>>(parent);
            Value.BrowseName = new QualifiedName(nameof(Value), namespaceIndex);
            Value.SymbolicName = nameof(Value);
            Value.TypeDefinitionId = VariableTypeIds.PropertyType;
            Value.DataType = DataTypeIds.BaseDataType;
            Value.ValueRank = ValueRanks.Scalar;
            Value.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            this.AddChild(Value);
        }

        public CustomEventState(NodeId nodeId, Func<NodeState, NodeId> createValueId, NodeState? parent, ushort namespaceIndex) : base(parent)
        {
            _nodeId = nodeId;
            Value = PropertyState<TValue>.With<EventValueBuilder<TValue>>(parent);
            Value.SymbolicName = nameof(Value);
            Value.BrowseName = new QualifiedName(nameof(Value), namespaceIndex);
            Value.DisplayName = new LocalizedText(nameof(Value));
            Value.TypeDefinitionId = VariableTypeIds.PropertyType;
            Value.DataType = DataTypeIds.BaseDataType;
            Value.ValueRank = ValueRanks.Scalar;
            Value.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            Value.NodeId = createValueId(Value);
            this.AddChild(this.Value);
        }

        protected override NodeId GetDefaultTypeDefinitionId(
            NamespaceTable namespaceUris)
        {
            return _nodeId;
        }

        public static BaseObjectTypeState CreateEventType<T>(NodeId nodeId, Func<NodeState, NodeId> createValueId, ushort namespaceIndex)
        where T : CustomEventState<TValue>
        {
            var eventType = new BaseObjectTypeState
            {
                NodeId = nodeId,
                BrowseName = new QualifiedName(typeof(T).Name.Replace("State", "Type"), namespaceIndex),
                DisplayName = new LocalizedText(typeof(T).Name.Replace("State", "Type")),
                IsAbstract = false,
                SuperTypeId = ObjectTypeIds.BaseEventType
            };

            eventType.AddReference(
                ReferenceTypeIds.HasSubtype,
                true,
                ObjectTypeIds.BaseEventType);


            var value = PropertyState<object>.With<EventValueBuilder<object>>(eventType);

            value.SymbolicName = nameof(Value);
            value.BrowseName = new QualifiedName(nameof(Value), namespaceIndex);
            value.DisplayName = new LocalizedText(nameof(Value));
            value.TypeDefinitionId = VariableTypeIds.PropertyType;
            value.DataType = DataTypeIds.BaseDataType;
            value.ValueRank = ValueRanks.Scalar;
            value.ModellingRuleId = ObjectIds.ModellingRule_Mandatory;
            value.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            value.NodeId = createValueId(value);

            eventType.AddChild(value);

            return eventType;
        }
    }
}
