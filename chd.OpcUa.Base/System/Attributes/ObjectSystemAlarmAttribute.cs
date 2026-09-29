using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Base.System.Attributes
{
    public class ObjectSystemAlarmAttribute : ObjectSystemEventAttribute
    {
        public bool Enabled { get; set; } = true;
    }
}
