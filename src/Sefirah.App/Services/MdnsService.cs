﻿using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Services;

public class MdnsService(ILogger logger) : IMdnsService
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
        try
        {
            if (serviceDiscovery != null && serviceProfile != null && multicastService != null)
            {
                logger.Info("Un-advertising service for {0}", serviceProfile.InstanceName);

                // Validate service instance name format
                if (string.IsNullOrWhiteSpace(serviceProfile.QualifiedServiceName.ToString()))
                {
                    logger.Warn("Service profile has invalid name, skipping unadvertise");
                    return;
                }

                // Library-specific cleanup sequence
                serviceDiscovery.Unadvertise(serviceProfile);
                multicastService.Stop();
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.Error("Service already unadvertised or invalid state", ex);
        }
        finally
        {
            // Dispose resources regardless of success
            serviceDiscovery?.Dispose();
            multicastService?.Dispose();
            
            // Reset references after disposal
            serviceDiscovery = null;
            multicastService = null;
            serviceProfile = null;
        }

        if (serviceDiscovery == null)
        {
            logger.Warn("Service already unadvertised or not initialized");
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
                        // Trim spaces in case there's any
                        var cleanTxtData = txtData.Trim();
                        var parts = cleanTxtData.Split(['='], 2); // Split at first '=' only
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
                        logger.Info($"Discovered service with ID: {deviceId}");
                        DiscoveredMdnsService?.Invoke(this, new DiscoveredMdnsServiceArgs 
                        { 
                            DeviceId = deviceId,  // Use just the device ID
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
            logger.Info("Started mDNS discovery service");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to start discovery service", ex);
            throw;
        }
    }
}