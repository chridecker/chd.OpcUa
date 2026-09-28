using chd.OpcUa.Contracts.Interfaces;
using chd.OpcUa.Contracts.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client.Subscriptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.Client.Extensions
{
    public static class DIExtensions
    {
        public static IServiceCollection AddOpcUaClient(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOpcUa()
                .ConfigureApplication(c =>
                {
                    c.ApplicationUri = "urn:localhost:UA:CHDClient";
                    c.AutoAcceptUntrustedCertificates = true;
                    c.ApplicationName = "CHD Client";
                    c.ConfigureSecurity = opt =>
                    {
                        opt.SetApplicationCertificates([ new CertificateIdentifier
                            {
                                StoreType = CertificateStoreType.Directory,
                                StorePath = Path.Combine(GetPrivateStateRoot(), "own"),
                                SubjectName = "CN=IntentViewerClient, O=OPC Foundation"
                            }]);
                    };
                })
                .AddClient(config =>
                {
                    config.AutoAcceptUntrustedCertificates = true;
                    config.ApplicationName = "CHD Client";
                });
            services.Configure<OpcUaClientConnectionOptions>(configuration.GetSection(nameof(OpcUaClientConnectionOptions)));
            services.Configure<SubscriptionOptions>(configuration.GetSection(nameof(SubscriptionOptions)));
            services.AddTransient<ITelemetryContext>(sp =>
                DefaultTelemetry.Create(c => c.SetMinimumLevel(LogLevel.Trace)));
            services.AddTransient<IOpcUAClient, OpcUaClient>();
            services.AddTransient<NotificationHandler>();

            return services;
        }

        private static string GetPrivateStateRoot()
        {
            string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDirectory))
            {
                baseDirectory = AppContext.BaseDirectory;
            }
            string root = Path.Combine(baseDirectory, "OPC Foundation", "IntentViewerClient", "pki");
            Directory.CreateDirectory(root);
            return root;
        }

    }
}
