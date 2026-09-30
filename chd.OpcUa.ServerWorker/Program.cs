using chd.OpcUa.Server;
using chd.OpcUa.Server.Extensions;
using chd.OpcUa.ServerWorker;
using Opc.Ua;
using Opc.Ua.Server;

var builder = Host.CreateApplicationBuilder(args);

//builder.Services.AddOpcUaServer<NamespaceManager, chdSystemManager>();chdObjectSystemManager
builder.Services.AddOpcUaServer<chdObjectSystemManager>(config =>
{
    config.ApplicationName = "CHD-Test-UAServer";
    config.ApplicationUri = "urn:localhost:CHD";
    config.Namespace = "urn:localhost:CHDNS";
    config.ManufacturerName = "CHD";
    config.Endpoints = ["opc.tcp://localhost/CHD/UaServer"];
}, roles =>
{
    roles.Roles.Add(new RoleDefinitionOptions()
    {
        Name = "Administrator",
        Identities =
        {
            new RoleIdentityMappingOptions()
            {
                Criteria = "admin",
                CriteriaType = IdentityCriteriaType.UserName
            }
        }
    });
    //roles.Roles.Add(new RoleDefinitionOptions()
    //{
    //    Name = "Supervisor",
    //    Identities =
    //    {
    //        new RoleIdentityMappingOptions()
    //        {
    //            Criteria = "supervisor",
    //            CriteriaType = IdentityCriteriaType.UserName
    //        }
    //    }
    //});
    //roles.Roles.Add(new RoleDefinitionOptions()
    //{
    //    Name = "User",
    //    Identities =
    //    {
    //        new RoleIdentityMappingOptions()
    //        {
    //            Criteria = "user",
    //            CriteriaType = IdentityCriteriaType.UserName
    //        }
    //    }
    //});
});


var host = builder.Build();
host.Run();
