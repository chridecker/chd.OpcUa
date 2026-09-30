using chd.OpcUa.Base.Extensions;
using chd.OpcUa.Base.States;
using chd.OpcUa.Server.Model;
using chd.OpcUa.Server.UnderlyingSystem;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using chd.OpcUa.ServerWorker;

namespace chd.OpcUa.Server.Extensions
{
    public static class EventBockStateExtensions
    {
        public static BaseEventState CreateEvent(this BlockState block, UnderlyingSystemEvent e, NodeManager nodeManager)
            => e.Type switch
            {
                var x when !x.Equals(typeof(void)) && x.IsValueType || x.Equals(typeof(string)) => CreateSimpleValueEvent(block, e, nodeManager),
                var x when !x.Equals(typeof(void)) && !x.IsValueType && x.IsClass => CreateObjectValueEvent(block, e, nodeManager),
                _ => CreateBaseEvent(block, e, nodeManager)
            };
        private static BaseEventState CreateBaseEvent(BlockState block, UnderlyingSystemEvent e, NodeManager nodeManager)
        {
            var evt = new BaseEventState(null);
            evt.Initialize(nodeManager.SystemContext, block, e.Severity, LocalizedText.From(e.Message));
            return evt;
        }

        private static SimpleValueCustomEventState CreateSimpleValueEvent(BlockState block, UnderlyingSystemEvent e, NodeManager nodeManager)
        {
            var evt = new SimpleValueCustomEventState(
                ModelUtils.ConstructIdForEventType<SimpleValueCustomEventState>(nodeManager.NamespaceIndex),
                state => ModelUtils.ConstructIdForComponent(state, nodeManager.NamespaceIndex), block,
                nodeManager.NamespaceIndex);
            evt.Initialize(nodeManager.SystemContext, block, e.Severity, LocalizedText.From(e.Message));

            evt.Value.Value = e.Value?.ConvertToVariant();
            return evt;
        }
        private static ComplexValueCustomEventState CreateComplexValueEvent(BlockState block, UnderlyingSystemEvent e, NodeManager nodeManager)
        {
            var evt = new ComplexValueCustomEventState(
                ModelUtils.ConstructIdForEventType<ComplexValueCustomEventState>(nodeManager.NamespaceIndex),
                state => ModelUtils.ConstructIdForComponent(state, nodeManager.NamespaceIndex), block,
                nodeManager.NamespaceIndex);
            evt.Initialize(nodeManager.SystemContext, block, e.Severity, LocalizedText.From(e.Message));

            evt.Value.Value = e.Value;
            return evt;
        }
        private static ObjectValueCustomEventState CreateObjectValueEvent(BlockState block, UnderlyingSystemEvent e, NodeManager nodeManager)
        {
            var evt = new ObjectValueCustomEventState(
                ModelUtils.ConstructIdForEventType<ObjectValueCustomEventState>(nodeManager.NamespaceIndex),
                state => ModelUtils.ConstructIdForComponent(state, nodeManager.NamespaceIndex), block,
                nodeManager.NamespaceIndex);
            evt.Initialize(nodeManager.SystemContext, block, e.Severity, LocalizedText.From(e.Message));

            var props = e.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            evt.Value.Value = props.Select(s => s.GetValue(e.Value)).ToArray();
            return evt;
        }
    }
}
