using chd.OpcUa.Contracts.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;
using Opc.Ua.Server;

namespace chd.OpcUa.Server.UnderlyingSystem
{
    public abstract class UnderlyingSystemBase : ISystemElement
    {

        public Dictionary<Role,PermissionType> Permissions { get; set; } = [];
        public string Name { get; }
        public string Description { get; set; }
        public virtual string Identifier => Name;

        protected UnderlyingSystemBase(string name)
        {
            Name = name;
        }
    }
}
