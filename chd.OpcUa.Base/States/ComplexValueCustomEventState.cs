using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;

namespace chd.OpcUa.Base.States
{
    public class ComplexValueCustomEventState : CustomEventState<ComplexTypeBuilder<object>,object>
    {
        public ComplexValueCustomEventState(NodeState? parent, ushort namespaceIndex) : base(parent, namespaceIndex)
        {
        }

        public ComplexValueCustomEventState(NodeId nodeId, Func<NodeState, NodeId> createValueId, NodeState? parent, ushort namespaceIndex) : base(nodeId, createValueId, parent, namespaceIndex)
        {
        }
    }
}
