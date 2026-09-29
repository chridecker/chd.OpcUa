using chd.OpcUa.Server;
using chd.OpcUa.Server.Extensions;
using chd.OpcUa.ServerWorker;

var builder = Host.CreateApplicationBuilder(args);

//builder.Services.AddOpcUaServer<NamespaceManager, chdSystemManager>();chdObjectSystemManager
builder.Services.AddOpcUaServer<chdSystemManager>(config =>
{
    config.ApplicationName = "CHD-Test-UAServer";
    config.ApplicationUri = "urn:localhost:CHD";
    config.Namespace = "urn:localhost:CHDNS";
    config.ManufacturerName = "CHD";
    config.Endpoints = ["opc.tcp://localhost/CHD/UaServer"];
});


var host = builder.Build();
host.Run();
