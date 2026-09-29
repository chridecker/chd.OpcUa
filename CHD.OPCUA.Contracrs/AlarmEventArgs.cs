using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace chd.OpcUa.Contracts
{
    public class AlarmEventArgs : EventArgs
    {
        public ReadOnlyMemory<byte> Id { get; set; }
        public uint Handle{ get; set; }
        public string Type { get; set; }
        public string SourceName { get; set; }
        public string ConditionName { get; set; }
        public string StateText { get; set; }
        public string Message { get; set; }
        public string Comment { get; set; }
        public bool IsDialog { get; set; }
        public string DialogText { get; set; }
        public ushort Severity { get; set; }
        public DateTime Time { get; set; }
        public bool IsAlarm { get; set; }
        public bool CanSilence { get; set; }
        public bool Retain { get; set; }
        public IReadOnlyList<string> DialogResponses { get; set; }
    }
}
