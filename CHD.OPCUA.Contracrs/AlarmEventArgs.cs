using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace chd.OpcUa.Contracts
{
    public class AlarmEventArgs : EventBase
    {
        public uint Handle{ get; set; }
        public bool Retain { get; set; }
        public bool Confirmed { get; set; }
        public bool Acknowledged { get; set; }
        public string Comment { get; set; }

    }
}
