using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Contracts
{
    public abstract class EventBase : EventArgs
    {
        public byte[] Id { get; set; }
        public string SourceName { get; set; }
        public DateTime Time { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public ushort Severity { get; set; }


    }
}
