﻿using Microsoft.Data.Sqlite;
using Sefirah.App.Data.AppDatabase.Models;
using System.Text.Json;
namespace Sefirah.App.Data.AppDatabase;
public class DeviceRepository(DatabaseContext context, ILogger logger)
{
    public async Task<List<RemoteDeviceEntity>> GetAllAsync()
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "SELECT DeviceId, DeviceName, IpAddresses, LastConnected, SharedSecret, WallpaperBytes FROM RemoteDevice",
                conn);

            using var reader = await command.ExecuteReaderAsync();
            var devices = new List<RemoteDeviceEntity>();

            while (await reader.ReadAsync())
            {
                devices.Add(new RemoteDeviceEntity
                {
                    DeviceId = reader.GetString(0),
                    Name = reader.GetString(1),
                    IpAddresses = reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<List<string>>(reader[2].ToString() ?? "[]"),
                    LastConnected = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    SharedSecret = reader.IsDBNull(4) ? null : (byte[])reader[4],
                    WallpaperBytes = reader.IsDBNull(5) ? null : (byte[])reader[5]
                });
            }

            return devices;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting devices", ex);
            throw;
        }
    }

    public async Task<RemoteDeviceEntity?> GetByIdAsync(string deviceId)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "SELECT DeviceId, DeviceName, IpAddresses, LastConnected, SharedSecret, WallpaperBytes " +
                "FROM RemoteDevice WHERE DeviceId = @DeviceId",
                conn);

            command.Parameters.AddWithValue("@DeviceId", deviceId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RemoteDeviceEntity
                {
                    DeviceId = reader.GetString(0),
                    Name = reader.GetString(1),
                    IpAddresses = reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<List<string>>(reader[2].ToString() ?? "[]"),
                    LastConnected = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    SharedSecret = reader.IsDBNull(4) ? null : (byte[])reader[4],
                    WallpaperBytes = reader.IsDBNull(5) ? null : (byte[])reader[5]
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting device", ex);
            throw;
        }
    }

    public async Task<RemoteDeviceEntity> AddOrUpdateAsync(RemoteDeviceEntity device)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "INSERT OR REPLACE INTO RemoteDevice " +
                "(DeviceId, DeviceName, IpAddresses, LastConnected, SharedSecret, WallpaperBytes) " +
                "VALUES (@DeviceId, @DeviceName, @IpAddresses, @LastConnected, @SharedSecret, @WallpaperBytes)",
                conn);

            command.Parameters.AddWithValue("@DeviceId", device.DeviceId);
            command.Parameters.AddWithValue("@DeviceName", device.Name);
            command.Parameters.AddWithValue("@IpAddresses", JsonSerializer.Serialize(device.IpAddresses));
            command.Parameters.AddWithValue("@LastConnected", device.LastConnected as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@SharedSecret", device.SharedSecret as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@WallpaperBytes", device.WallpaperBytes as object ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
            return device;
        }
        catch (Exception ex)
        {
            logger.Error("Error adding/updating device", ex);
            throw;
        }
    }

    public async Task DeleteAsync(string deviceId)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "DELETE FROM RemoteDevice WHERE DeviceId = @DeviceId",
                conn);

            command.Parameters.AddWithValue("@DeviceId", deviceId);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error deleting device", ex);
            throw;
        }
    }

    public async Task<RemoteDeviceEntity?> GetLastConnectedDeviceAsync()
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "SELECT DeviceId, DeviceName, IpAddresses, LastConnected, SharedSecret, WallpaperBytes " +
                "FROM RemoteDevice ORDER BY LastConnected DESC LIMIT 1",
                conn);

            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var WallpaperBytes = reader.IsDBNull(5) ? null : (byte[])reader[5];
                var WallpaperImage = WallpaperBytes?.ToBitmap();

                return new RemoteDeviceEntity
                {
                    DeviceId = reader.GetString(0),
                    Name = reader.GetString(1),
                    IpAddresses = reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<List<string>>(reader[2].ToString() ?? "[]"),
                    LastConnected = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    SharedSecret = reader.IsDBNull(4) ? null : (byte[])reader[4],
                    WallpaperBytes = WallpaperBytes,
                    WallpaperImage = WallpaperImage
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.Error("Error getting last connected device", ex);
            return null;
        }
    }

    public async Task<LocalDeviceEntity> AddOrUpdateLocalDeviceAsync(LocalDeviceEntity device)
    {
        try
        {
            var conn = await context.GetConnectionAsync();
            var command = new SqliteCommand(
                "INSERT OR REPLACE INTO LocalDevice (DeviceId, DeviceName, PublicKey, PrivateKey) VALUES (@DeviceId, @DeviceName, @PublicKey, @PrivateKey)",
                conn);

            command.Parameters.AddWithValue("@DeviceId", device.DeviceId);
            command.Parameters.AddWithValue("@DeviceName", device.DeviceName);
            command.Parameters.AddWithValue("@PublicKey", device.PublicKey);
            command.Parameters.AddWithValue("@PrivateKey", device.PrivateKey);

            await command.ExecuteNonQueryAsync();
            return device;
        }
        catch (Exception ex)
        {
            logger.Error("Error adding/updating local device", ex);
            throw;
        }
    }

    public async Task<LocalDeviceEntity?> GetLocalDevice()
    {
        var conn = await context.GetConnectionAsync();
        var command = new SqliteCommand("SELECT * FROM LocalDevice", conn);
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new LocalDeviceEntity
            {
                DeviceId = reader.GetString(0),
                DeviceName = reader.GetString(1),
                PublicKey = (byte[])reader[2],
                PrivateKey = (byte[])reader[3]
            };
        }
        return null;
    }
}