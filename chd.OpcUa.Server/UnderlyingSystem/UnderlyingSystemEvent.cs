using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Contracts;
using Opc.Ua;

namespace chd.OpcUa.Server.UnderlyingSystem
{
    public class UnderlyingSystemEvent : UnderlyingSystemBase
    {
        public string Message { get; set; }
        public object Value { get; set; }
        public Type Type{ get; set; }
        public EventSeverity Severity { get; set; }

        public UnderlyingSystemEvent(string name, string description) : base(name)
        {
            Description = description;
        }

        public UnderlyingSystemEvent CreateSnapshot() => (UnderlyingSystemEvent)MemberwiseClone();
    }
}
