using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;

namespace chd.OpcUa.Base.States
{
    public class ObjectValueCustomEventState : CustomEventState<ComplexTypeBuilder<object[]>, object[]>
    {
        public ObjectValueCustomEventState(NodeState? parent, ushort namespaceIndex) : base(parent, namespaceIndex)
        {
        }

        public ObjectValueCustomEventState(NodeId nodeId, Func<NodeState, NodeId> createValueId, NodeState? parent, ushort namespaceIndex) : base(nodeId, createValueId, parent, namespaceIndex)
        {
        }
    }
}
