using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Server.UnderlyingSystem
{
    public class UnderlyingSystemAlarm : UnderlyingSystemEvent
    {
        public string Comment { get; set; } = string.Empty;
        public bool Enabled { get; set; } 
        public bool Confirmed { get; set; }
        public bool Acknowledged { get; set; }
        public bool Active => !Confirmed || !Acknowledged;

        public Guid? Id { get; set; }

        public UnderlyingSystemAlarm(string name, string description) : base(name, description, typeof(string))
        {
        }
    }
}
