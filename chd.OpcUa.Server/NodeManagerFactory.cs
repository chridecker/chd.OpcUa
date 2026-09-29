using chd.OpcUa.Contracts.Interfaces;
using chd.OpcUa.Server.UnderlyingSystem;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Server.Options;
using Microsoft.Extensions.Options;

namespace chd.OpcUa.ServerWorker
{
    public class NodeManagerFactory(IUnderlyingSystemManager<UnderlyingSystemSegment, UnderlyingSystemBlock, UnderlyingSystemMethod> underlyingSystemManager,
        IOptions<ServerOptions> serverOptions) : IAsyncNodeManagerFactory
    {
        public ValueTask<IAsyncNodeManager> CreateAsync(IServerInternal server, ApplicationConfiguration configuration,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return new ValueTask<IAsyncNodeManager>(new NodeManager(server, configuration, underlyingSystemManager, serverOptions.Value.Namespace));
        }

        public ArrayOf<string> NamespacesUris => [serverOptions.Value.Namespace];
    }
}
