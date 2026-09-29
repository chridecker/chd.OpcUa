using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Base.System.Attributes
{
    public class ObjectSystemPropertyAttribute(string displayName = null) : ObjectSystemAttribute(displayName)
    {
        public bool CanWrite { get; set; }
    }
}
