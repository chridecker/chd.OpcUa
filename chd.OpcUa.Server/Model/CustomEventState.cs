using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;

namespace chd.OpcUa.Server.Model
{
    public class CustomEventState : BaseEventState
    {
        private readonly ushort _namespaceIndex;
        public EventValueState? Value { get; set; }
        public CustomEventState(NodeState? parent, ushort namespaceIndex) : base(parent)
        {
            _namespaceIndex = namespaceIndex;
            Value = new(this)
            {
                BrowseName = new QualifiedName(nameof(Value), namespaceIndex),
                DisplayName = new LocalizedText(nameof(Value)),
                TypeDefinitionId = VariableTypeIds.PropertyType,
                DataType = DataTypeIds.BaseDataType,
                ValueRank = ValueRanks.Scalar,
                ReferenceTypeId = ReferenceTypeIds.HasProperty,
            };
            this.AddChild(this.Value);
        }
        protected override NodeId GetDefaultTypeDefinitionId(
            NamespaceTable namespaceUris)
        {
            return ModelUtils.ConstructIdForEventType<CustomEventState>(_namespaceIndex);
        }
    }
}
