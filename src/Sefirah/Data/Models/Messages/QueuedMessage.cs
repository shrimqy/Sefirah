namespace Sefirah.Data.Models.Messages;

/// <summary>Outbound pending send: UI message plus recipient addresses for wire rebuild.</summary>
public readonly record struct QueuedMessage(string DeviceId, Message Message, IReadOnlyList<string> Addresses);
