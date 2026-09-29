using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Base.System.Attributes
{
    public abstract class ObjectSystemAttribute : Attribute
    {
        public string DisplayName { get; }
        public string Description { get; set; }
        protected ObjectSystemAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
