using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Server.Model
{
    public static class ModelUtils
    {
        public const int Segment = 0;

        public const int Block = 1;

        public const int Method = 2;

        public const int InputArgument = 3;

        public const int OutputArgument = 4;
        public const int EventType = 5;

        public static NodeId ConstructIdForSegment(string identifier, ushort namespaceIndex)
        {
            var parsedNodeId = new ParsedNodeId
            {
                RootId = identifier,
                NamespaceIndex = namespaceIndex,
                RootType = Segment,
            };
            return parsedNodeId.Construct();
        }

        public static NodeId ConstructIdForBlock(string blockId, ushort namespaceIndex)
        {
            var parsedNodeId = new ParsedNodeId
            {
                RootId = blockId,
                NamespaceIndex = namespaceIndex,
                RootType = Block
            };
            return parsedNodeId.Construct();
        }

        public static NodeId ConstructIdForMethod(string methodId, ushort namespaceIndex)
        {
            var parsedNodeId = new ParsedNodeId
            {
                RootId = methodId,
                NamespaceIndex = namespaceIndex,
                RootType = Method
            };
            return parsedNodeId.Construct();
        }
        public static NodeId ConstructIdForInputArguments(string argumentsId, ushort namespaceIndex)
        {
            var parsedNodeId = new ParsedNodeId
            {
                RootId = argumentsId + ":Input",
                NamespaceIndex = namespaceIndex,
                RootType = InputArgument
            };
            return parsedNodeId.Construct();
        }

        public static NodeId ConstructIdForOutputArguments(string argumentsId, ushort namespaceIndex)
        {
            var parsedNodeId = new ParsedNodeId
            {
                RootId = argumentsId + ":Output",
                NamespaceIndex = namespaceIndex,
                RootType = OutputArgument
            };
            return parsedNodeId.Construct();
        }

        public static NodeId ConstructIdForEventType<T>(ushort namespaceIndex)
        where T : BaseEventState
        {
            var parsedNodeId = new ParsedNodeId
            {
                RootId = typeof(T).Name.Replace("State", "Type"),
                NamespaceIndex = namespaceIndex,
                RootType = EventType
            };
            return parsedNodeId.Construct();
        }

        public static BaseObjectTypeState CreateEventType<T>(
            ushort namespaceIndex)
            where T : CustomEventState
        {
            var eventType = new BaseObjectTypeState
            {
                NodeId = ConstructIdForEventType<T>(namespaceIndex),
                BrowseName = new QualifiedName(typeof(T).Name.Replace("State", "Type"), namespaceIndex),
                DisplayName = new LocalizedText(typeof(T).Name.Replace("State", "Type")),
                IsAbstract = false,
                SuperTypeId = ObjectTypeIds.BaseEventType
            };

            eventType.AddReference(
                ReferenceTypeIds.HasSubtype,
                true,
                ObjectTypeIds.BaseEventType);


            // InstanceDeclaration "Value"
            var value = new EventValueState(eventType)
            {
                SymbolicName = "Value",
                BrowseName = new QualifiedName("Value", namespaceIndex),
                DisplayName = new LocalizedText("Value"),
                TypeDefinitionId = VariableTypeIds.PropertyType,
                DataType = DataTypeIds.BaseDataType,
                ValueRank = ValueRanks.Scalar,
                ModellingRuleId = ObjectIds.ModellingRule_Mandatory,
                ReferenceTypeId = ReferenceTypeIds.HasProperty,
            };
            value.NodeId = ConstructIdForComponent(value, namespaceIndex);

            eventType.AddChild(value);

            return eventType;
        }

        public static NodeId ConstructIdForComponent(NodeState component, ushort namespaceIndex)
        {
            if (component is null)
            {
                return NodeId.Null;
            }

            var instance = component as BaseInstanceState;

            if (instance?.Parent is null)
            {
                return component.NodeId;
            }

            var parentId = instance.Parent.NodeId.TryGetValue(out string id) ? id : null;

            if (parentId is null)
            {
                return NodeId.Null;
            }

            var buffer = new StringBuilder();
            buffer.Append(parentId);

            var index = parentId.IndexOf('?', StringComparison.Ordinal);

            if (index < 0)
            {
                buffer.Append('?');
            }
            else
            {
                buffer.Append('/');
            }

            buffer.Append(component.SymbolicName);

            return new NodeId(buffer.ToString(), namespaceIndex);
        }
    }
}
