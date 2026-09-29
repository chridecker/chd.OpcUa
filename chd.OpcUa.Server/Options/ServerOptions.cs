using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Server.Options
{
    public class ServerOptions
    {
        public string ManufacturerName { get; set; }
        public string ApplicationName { get; set; }
        public string ApplicationUri { get; set; }
        public string[] Endpoints { get; set; }
        public string Namespace { get; set; }
    }
}
