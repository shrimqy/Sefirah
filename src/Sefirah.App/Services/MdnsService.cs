using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.Utils;

namespace Sefirah.App.Services;

public class MdnsService(ILogger logger) : IMdnsService
{
    private MulticastService? multicastService;
    private ServiceProfile? serviceProfile;
    private ServiceDiscovery? serviceDiscovery;

    public event EventHandler<DiscoveredMdnsServiceArgs>? DiscoveredMdnsService;
    public event EventHandler<ServiceInstanceShutdownEventArgs>? ServiceInstanceShutdown;

    /// <inheritdoc />
    public void AdvertiseService(int port)
    {
        try
        {
            // Fetch device Id
            var deviceId = CurrentUserInformation.GenerateDeviceId();
            // Set up the service profile
            serviceProfile = new ServiceProfile(deviceId, "_sefirah._udp", ((ushort)port));

            // Advertise the service
            multicastService = new MulticastService();
            serviceDiscovery = new ServiceDiscovery(multicastService);
            serviceDiscovery.Advertise(serviceProfile);

            logger.Info("Advertising service for {0}", serviceProfile.InstanceName);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to advertise service", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public void UnAdvertiseService()
    {
        if (serviceDiscovery != null && serviceProfile != null)
        {
            logger.Info("Un-advertising service for {0}", serviceProfile.InstanceName);
            serviceDiscovery.Unadvertise(serviceProfile);
        }
        else
        {
            logger.Warn("Service not advertised or already unadvertised");
        }
    }

    /// <inheritdoc />
    public void StartDiscovery()
    {
        try
        {
            if (serviceDiscovery == null || multicastService == null) return;

            serviceDiscovery.ServiceInstanceDiscovered += (sender, args) =>
            {
                // Ignore our own service instance
                if (serviceProfile != null && args.ServiceInstanceName == serviceProfile.FullyQualifiedName) return;
                
                // Only process _sefirah._udp services
                if (!args.ServiceInstanceName.ToCanonical().ToString().Contains("_sefirah._udp")) return;
                
                // Query for both TXT and SRV records
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.TXT);
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.SRV);
                
            };

            // Add handler for answers
            multicastService.AnswerReceived += (sender, args) => {
                foreach (var answer in args.Message.Answers)
                {
                    if (answer is SRVRecord srvRecord)
                    {
                        // Only process _sefirah._udp services
                        if (!srvRecord.CanonicalName.Contains("_sefirah._udp")) continue;
                        
                        int port = srvRecord.Port;
                        DiscoveredMdnsService?.Invoke(this, new DiscoveredMdnsServiceArgs { ServiceInstanceName = srvRecord.CanonicalName, Port = port });
                    }
                }
            };

            serviceDiscovery.ServiceInstanceShutdown += (sender, args) =>
            {
                ServiceInstanceShutdown?.Invoke(this, args);
            };

            multicastService.Start();
            logger.Info("Started mDNS discovery service");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to start discovery service", ex);
            throw;
        }
    }
}