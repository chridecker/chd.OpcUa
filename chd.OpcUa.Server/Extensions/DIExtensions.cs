using chd.OpcUa.Contracts.Interfaces;
using chd.OpcUa.Server.Options;
using chd.OpcUa.Server.UnderlyingSystem;
using chd.OpcUa.ServerWorker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Server;
using Opc.Ua.Server.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Server.Extensions
{
    public static class DIExtensions
    {
        public static IServiceCollection AddOpcUaServer<TSystemManager>(this IServiceCollection services,
            Action<ServerOptions> serverConfig)
            where TSystemManager : UnderlyingSystemManager
        {
            if (serverConfig is not null)
            {
                services.Configure<ServerOptions>(serverConfig);
            }


            var server = services.AddOpcUa()
                 .AddServer(config =>
                 {
                     ServerOptions localConfig = null;
                     if (serverConfig is not null)
                     {
                         localConfig = new();
                         serverConfig(localConfig);
                     }


                     config.ApplicationName = localConfig?.ApplicationName ?? nameof(UaServer);
                     config.ApplicationUri = localConfig?.ApplicationUri ?? "urn:localhost:UA:CHDUaServer";

                     if (localConfig?.Endpoints.Any() ?? false)
                     {
                         foreach (var localConfigEndpoint in localConfig.Endpoints)
                         {
                             config.EndpointUrls.Add(localConfigEndpoint);
                         }
                     }
                     else
                     {
                         config.EndpointUrls.Add("opc.tcp://localhost:4840/CHD/UaServer");
                     }
                     config.AutoAcceptUntrustedCertificates = true;
                     config.IncludeUnsecurePolicyNone = true;
                 })
                 .AddNodeManager<NodeManagerFactory>()
                 .ConfigureRoles(roles =>
                 {
                     //roles.Roles.Add(new RoleDefinitionOptions()
                     //{
                     //    Name = "Administrator",
                     //    Identities =
                     //    {
                     //        new RoleIdentityMappingOptions()
                     //        {
                     //            Criteria = "admin",
                     //            CriteriaType = IdentityCriteriaType.UserName
                     //        }
                     //}
                     //});
                 })
                 .AddIdentityAuthenticator((_, _) => new UserNamePasswordAuthenticator((handler, ct) =>
                 {
                     var password = handler.DecryptedPassword != null
                         ? Encoding.UTF8.GetString(handler.DecryptedPassword)
                         : null;

                     if (string.Equals(handler.UserName, "admin", StringComparison.OrdinalIgnoreCase)
                         && string.Equals(password, "1234", StringComparison.Ordinal))
                     {
                         return new ValueTask<IUserIdentity>(new UserIdentity(handler));
                     }
                     throw ServiceResultException.Create(
                         StatusCodes.BadUserAccessDenied,
                         "'{0}' is not one of the sample accounts, or the password is wrong.",
                         handler.UserName);
                 }));

            services.TryAddSingleton(TimeProvider.System);
            services.AddTransient<ITelemetryContext>(sp =>
                DefaultTelemetry.Create(c => c.SetMinimumLevel(LogLevel.Trace)));
            services.TryAddSingleton<UaServer>();
            services.TryAddSingleton<UaServerFactory>();
            services.TryAddSingleton<NodeManagerFactory>();
            services.Replace(ServiceDescriptor.Singleton<IAsyncNodeManagerFactory>(
                provider => provider.GetService<NodeManagerFactory>()));


            // the forms of the server samples show the running server and take the
            // shared StandardServer, so they do not have to know the server class.
            services.AddSingleton<StandardServer>(
                provider => provider.GetRequiredService<UaServer>());

            // the hosted server creates its server through this factory: the instance
            // of the sample from the container, which the main form resolves too.
            services.Replace(ServiceDescriptor.Singleton<IOpcUaServerFactory>(
                provider => provider.GetService<UaServerFactory>()));


            // registered after the parts of the sample, so its startup tasks have run
            // when the start of the host returns with the server listening - which the
            // samples and their tests rely on.
            services.AddSingleton<UaServerStartup>();
            services.AddSingleton<IServerStartupTask, UaServerStartup>();
            services.AddSingleton<IUnderlyingSystemManager<UnderlyingSystemSegment, UnderlyingSystemBlock, UnderlyingSystemMethod>, TSystemManager>();

            return services;
        }
    }
}
