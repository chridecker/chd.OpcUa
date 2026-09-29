using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Base.System.Attributes
{
    public class ObjectSystemMethodAttribute(string displayName = null) : ObjectSystemAttribute(displayName)
    {
        public bool CanExecute { get; set; } = true;
    }
}
