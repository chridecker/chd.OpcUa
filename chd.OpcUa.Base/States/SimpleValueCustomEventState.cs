using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;

namespace chd.OpcUa.Base.States
{
    public class SimpleValueCustomEventState : CustomEventState<object>
    {
        public SimpleValueCustomEventState(NodeState? parent, ushort namespaceIndex) : base(parent, namespaceIndex)
        {
        }

        public SimpleValueCustomEventState(NodeId nodeId, Func<NodeState, NodeId> createValueId, NodeState? parent, ushort namespaceIndex) : base(nodeId, createValueId, parent, namespaceIndex)
        {
        }
    }
}
