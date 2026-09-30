using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Server.Authentication
{
    public class AuthenticationHandler
    {
        public bool IsValid(string username, string password) => (username.ToLower(), password) switch
        {
            ("admin", "3510") => true,
            ("superuser", "1234") => true,
            ("user", _) => true,
            _ => false
        };


    }
}
