using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Contracts.Options
{
    public class OpcUaClientConnectionOptions
    {
        public string Name { get; set; }
        public string EndpointUrl { get; set; }
        public TimeSpan? Timeout { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseCertificate { get; set; }
        public string[] StartNodes { get; set; }
    }
}
