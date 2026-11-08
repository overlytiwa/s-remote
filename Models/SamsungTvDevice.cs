using System;
using System.Text.Json.Serialization;

namespace SamsungMiniRemote.Models;

/// <summary>
/// Represents a discovered or saved Samsung Smart TV on the local network.
/// </summary>
public sealed class SamsungTvDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 8002;
    public string? MacAddress { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name :
        !string.IsNullOrWhiteSpace(Model) ? Model :
        IpAddress;

    [JsonIgnore]
    public string DropdownLabel =>
        !string.IsNullOrWhiteSpace(Name)
            ? $"{Name} ({IpAddress})"
            : !string.IsNullOrWhiteSpace(Model)
                ? $"{Model} ({IpAddress})"
                : IpAddress;

    public override bool Equals(object? obj) =>
        obj is SamsungTvDevice other && string.Equals(IpAddress, other.IpAddress, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(IpAddress);
}

