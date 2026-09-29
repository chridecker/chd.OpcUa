using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Base.System.Attributes
{
    public class ObjectSystemAlarmAttribute : ObjectSystemEventAttribute
    {
        public bool Retain { get; set; } = true;
        public TimeSpan RealarmTime { get; set; }
    }
}
