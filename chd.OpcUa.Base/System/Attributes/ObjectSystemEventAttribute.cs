using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;

namespace chd.OpcUa.Base.System.Attributes
{
    public class ObjectSystemEventAttribute(string displayName = null) : ObjectSystemAttribute(displayName)
    {
        public string SeverityMethod { get; set; }
    }
}
