using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Contracts
{
    public class SimpleEventArgs : EventBase
    {

        public object Value { get; set; }
    }
}
