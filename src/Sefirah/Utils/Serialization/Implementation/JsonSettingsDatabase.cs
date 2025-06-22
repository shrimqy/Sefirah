using System.Collections.Concurrent;
using System.Text.Json;

namespace Sefirah.Utils.Serialization.Implementation;
internal class JsonSettingsDatabase(
    ISettingsSerializer settingsSerializer, 
    IJsonSettingsSerializer jsonSettingsSerializer
) : IJsonSettingsDatabase
{

    protected ISettingsSerializer SettingsSerializer { get; } = settingsSerializer;

    protected IJsonSettingsSerializer JsonSettingsSerializer { get; } = jsonSettingsSerializer;

    protected IDictionary<string, object?> GetFreshSettings()
    {
        string data = SettingsSerializer.ReadFromFile();

        if (string.IsNullOrWhiteSpace(data))
        {
            data = "null";
        }

        try
        {
            return JsonSettingsSerializer.DeserializeFromJson<ConcurrentDictionary<string, object?>?>(data) ?? new();
        }
        catch (Exception)
        {
            // Occurs if the settings file has invalid json
            // TODO Display prompt to notify user #710
            return JsonSettingsSerializer.DeserializeFromJson<ConcurrentDictionary<string, object?>?>("null") ?? new();
        }
    }

    protected bool SaveSettings(IDictionary<string, object?> data)
    {
        var jsonData = JsonSettingsSerializer.SerializeToJson(data);

        return SettingsSerializer.WriteToFile(jsonData);
    }

    public virtual TValue? GetValue<TValue>(string key, TValue? defaultValue = default)
    {
        var data = GetFreshSettings();

        if (data.TryGetValue(key, out var objVal))
        {
            return GetValueFromObject<TValue>(objVal) ?? defaultValue;
        }
        else
        {
            SetValue(key, defaultValue);
            return defaultValue;
        }
    }

    public virtual bool SetValue<TValue>(string key, TValue? newValue)
    {
        var data = GetFreshSettings();

        if (!data.TryAdd(key, newValue))
            data[key] = newValue;

        return SaveSettings(data);
    }

    public virtual bool RemoveKey(string key)
    {
        var data = GetFreshSettings();

        return data.Remove(key) && SaveSettings(data);
    }

    public bool FlushSettings()
    {
        // The settings are always flushed automatically, return true.
        return true;
    }

    protected static TValue? GetValueFromObject<TValue>(object? obj)
    {
        if (obj is JsonElement jElem)
        {
            return jElem.Deserialize<TValue>();
        }

        return (TValue?)obj;
    }
}
