using System.Net.Sockets;
using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;
using Sefirah.Data.Contracts;
using Sefirah.Data.EventArguments;
using Sefirah.Data.Models;

namespace Sefirah.Services;

public class MdnsService(ILogger logger) : IMdnsService
{
    private const string ServiceType = "_sefirah._udp";
    private const string DeviceNameProperty = "deviceName";
    private const string PublicKeyProperty = "publicKey";
    private const string ServerPortProperty = "serverPort";

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
            serviceProfile = new ServiceProfile(broadcast.DeviceId, ServiceType, (ushort)port);
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
        }
    }

    /// <inheritdoc />
    public void UnAdvertiseService()
    {
        try
        {
            if (serviceDiscovery is not null && serviceProfile is not null && multicastService is not null)
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
    }


    /// <inheritdoc />
    public void StartDiscovery()
    {
        try
        {
            if (serviceDiscovery is null || multicastService is null) return;

            serviceDiscovery.ServiceInstanceDiscovered += (sender, args) =>
            {
                // Ignore our own service instance
                if (serviceProfile is not null && args.ServiceInstanceName == serviceProfile.FullyQualifiedName) return;
                
                // Only process _sefirah._udp services
                if (!args.ServiceInstanceName.ToCanonical().ToString().Contains(ServiceType)) return;
                // Queries
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.TXT);
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.SRV);
                multicastService.SendQuery(args.ServiceInstanceName, type: DnsType.A);

            };

            // Add handler for answers
            multicastService.AnswerReceived += (sender, args) => {
                var txtRecords = args.Message.Answers.OfType<TXTRecord>();
                var srvRecords = args.Message.Answers.OfType<SRVRecord>()
                    .Concat(args.Message.AdditionalRecords.OfType<SRVRecord>())
                    .ToList();
                var aRecords = args.Message.Answers.OfType<AddressRecord>()
                    .Concat(args.Message.AdditionalRecords.OfType<AddressRecord>())
                    .Where(a => a.Address is not null && a.Address.AddressFamily is AddressFamily.InterNetwork)
                    .ToList();
                
                // Process TXT records to get device information
                foreach (var txtRecord in txtRecords)
                {
                    string? deviceName = null;
                    string? publicKey = null;
                    int? port = null;

                    // Only process _sefirah._udp services
                    if (!txtRecord.CanonicalName.Contains(ServiceType)) continue;

                    foreach (var txtData in txtRecord.Strings)
                    {
                        var cleanTxtData = txtData.Trim();
                        var parts = cleanTxtData.Split(['='], 2); // Split at first '=' 
                        if (parts.Length == 2)
                        {
                            if (parts[0] == DeviceNameProperty)
                                deviceName = parts[1];
                            else if (parts[0] == PublicKeyProperty)
                                publicKey = parts[1];
                            else if (parts[0] == ServerPortProperty && int.TryParse(parts[1], out var parsedPort))
                                port = parsedPort;
                        }
                    }

                    if (!string.IsNullOrEmpty(deviceName) && !string.IsNullOrEmpty(publicKey) && txtRecord.CanonicalName != serviceProfile!.FullyQualifiedName)
                    {
                        var deviceId = txtRecord.CanonicalName.Split('.')[0];
                        
                        // Get hostname from SRV record, then find matching A record
                        var srvRecord = srvRecords.FirstOrDefault(s => s.CanonicalName.Contains(deviceId));
                        if (srvRecord?.Target is not null)
                        {
                            var hostname = srvRecord.Target.ToString();
                            var aRecord = aRecords.FirstOrDefault(a => 
                                a.CanonicalName.ToString().Equals(hostname.ToString(), StringComparison.OrdinalIgnoreCase));
                            if (aRecord?.Address is not null)
                            {
                                var address = aRecord.Address.ToString();
                                // Use SRV port as fallback if serverPort not in TXT
                                var finalPort = port ?? srvRecord.Port;
                                DiscoveredMdnsService?.Invoke(this, new(deviceId, deviceName, publicKey, address, finalPort));
                            }
                        }
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
        }
    }
}
