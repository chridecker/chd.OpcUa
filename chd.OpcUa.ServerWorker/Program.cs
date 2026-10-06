using chd.OpcUa.Server;
using chd.OpcUa.Server.Extensions;
using chd.OpcUa.ServerWorker;
using Opc.Ua;
using Opc.Ua.Server;

var builder = Host.CreateApplicationBuilder(args);

//builder.Services.AddOpcUaServer<NamespaceManager, chdSystemManager>();chdObjectSystemManager
builder.Services.AddOpcUaServer<chdSystemManager>(config =>
{
    config.ApplicationName = "CHD-Test-UAServer";
    config.ApplicationUri = "urn:localhost:CHD";
    config.Namespace = "urn:localhost:CHDNS";
    config.ManufacturerName = "CHD";
    config.Endpoints = ["opc.tcp://localhost/CHD/UaServer"];
    config.AutoAcceptUntrustedCertificates = true;
    config.IncludeUnsecurePolicyNone = true;
}, Authenticator.ConfigureRoles, Authenticator.AuthenticateAsync);


var host = builder.Build();
host.Run();
