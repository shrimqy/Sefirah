using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;
using Sefirah.Data.Contracts;
using Sefirah.Data.EventArguments;
using Sefirah.Data.Models;

namespace Sefirah.Services;

public class MdnsService(ILogger<MdnsService> logger) : IMdnsService
{
    private MulticastService? multicastService;
    private ServiceProfile? serviceProfile;
    private ServiceDiscovery? serviceDiscovery;

    public event EventHandler<DiscoveredMdnsServiceArgs>? DiscoveredMdnsService;
    public event EventHandler<ServiceInstanceShutdownEventArgs>? ServiceInstanceShutdown;

    /// <inheritdoc />
    public void AdvertiseService(UdpBroadcast broadcast, int port)
    {
        try
        {
            // Set up the service profile
            serviceProfile = new ServiceProfile(broadcast.DeviceId, "_sefirah._udp", ((ushort)port));
            serviceProfile.AddProperty("deviceName", broadcast.DeviceName);
            serviceProfile.AddProperty("publicKey", broadcast.PublicKey);
            serviceProfile.AddProperty("serverPort", broadcast.Port.ToString());

            // Advertise the service
            multicastService = new MulticastService();
            serviceDiscovery = new ServiceDiscovery(multicastService);
            serviceDiscovery.Advertise(serviceProfile);

            logger.LogInformation("Advertising service for {name}", serviceProfile.InstanceName);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to advertise service {ex}", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public void UnAdvertiseService()
    {
        try
        {
            if (serviceDiscovery != null && serviceProfile != null && multicastService != null)
            {
                logger.LogInformation("Un-advertising service for {0}", serviceProfile.InstanceName);

                // Validate service instance name format
                if (string.IsNullOrWhiteSpace(serviceProfile.QualifiedServiceName.ToString()))
                {
                    logger.LogWarning("Service profile has invalid name, skipping unadvertise");
                    return;
                }
                serviceDiscovery.Unadvertise(serviceProfile);
                multicastService.Stop();
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.LogError("Service already unadvertised or invalid state: {ex}", ex);
        }
        finally
        {
            serviceDiscovery?.Dispose();
            multicastService?.Dispose();
            serviceDiscovery = null;
            multicastService = null;
            serviceProfile = null;
        }

        if (serviceDiscovery == null)
        {
            logger.LogWarning("Service already unadvertised or not initialized");
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

                // Queries
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.TXT);
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.SRV);
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.A);

            };

            // Add handler for answers
            multicastService.AnswerReceived += (sender, args) => {
                var txtRecords = args.Message.Answers.OfType<TXTRecord>();
                foreach (var txtRecord in txtRecords)
                {
                    string? deviceName = null;
                    string? publicKey = null;

                    // Only process _sefirah._udp services
                    if (!txtRecord.CanonicalName.Contains("_sefirah._udp")) continue;

                    foreach (var txtData in txtRecord.Strings)
                    {
                        var cleanTxtData = txtData.Trim();
                        var parts = cleanTxtData.Split(['='], 2); // Split at first '=' 
                        if (parts.Length == 2)
                        {
                            if (parts[0] == "deviceName")
                            {
                                deviceName = parts[1];
                            }
                            else if (parts[0] == "publicKey")
                            {
                                publicKey = parts[1];
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(deviceName) && !string.IsNullOrEmpty(publicKey) && txtRecord.CanonicalName != serviceProfile!.FullyQualifiedName)
                    {
                        var deviceId = txtRecord.CanonicalName.Split('.')[0]; // Split on first dot to get device ID
                        DiscoveredMdnsService?.Invoke(this, new DiscoveredMdnsServiceArgs 
                        { 
                            DeviceId = deviceId, 
                            DeviceName = deviceName, 
                            PublicKey = publicKey 
                        });
                    }

                }
            };

            serviceDiscovery.ServiceInstanceShutdown += (sender, args) =>
            {
                ServiceInstanceShutdown?.Invoke(this, args);
            };

            multicastService.Start();
            logger.LogInformation("Started mDNS discovery service");
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to start discovery service: {ex}", ex);
            throw;
        }
    }
}
