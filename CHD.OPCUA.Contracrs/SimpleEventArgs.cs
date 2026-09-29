using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Contracts
{
    public class SimpleEventArgs : EventBase
    {

        public string Message { get; set; }
        public object Value { get; set; }
    }
}
