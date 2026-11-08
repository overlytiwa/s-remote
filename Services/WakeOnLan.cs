using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SamsungMiniRemote.Services;

/// <summary>
/// Sends Wake-on-LAN magic packets to attempt powering on a Samsung TV.
/// Works for TVs that support WoL / WoWLAN when in standby.
/// </summary>
public static class WakeOnLan
{
    /// <summary>
    /// Sends a WoL magic packet to the given MAC address.
    /// </summary>
    /// <param name="macAddress">MAC address in any standard format (e.g., "AA:BB:CC:DD:EE:FF" or "AA-BB-CC-DD-EE-FF").</param>
    public static async Task SendAsync(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return;

        var mac = ParseMac(macAddress);
        if (mac == null || mac.Length != 6) return;

        // Build the magic packet: 6 bytes of 0xFF followed by the MAC repeated 16 times.
        var packet = new byte[6 + 16 * 6];
        for (int i = 0; i < 6; i++)
            packet[i] = 0xFF;
        for (int i = 0; i < 16; i++)
            Buffer.BlockCopy(mac, 0, packet, 6 + i * 6, 6);

        using var client = new UdpClient();
        client.EnableBroadcast = true;
        // Send to subnet broadcast and the limited broadcast.
        await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
        await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 7));
    }

    /// <summary>
    /// Checks whether a host is reachable via a quick TCP connect to the WebSocket port.
    /// </summary>
    public static async Task<bool> IsReachableAsync(string ip, int port = 8002, int timeoutMs = 1500)
    {
        try
        {
            using var tcp = new TcpClient();
            var task = tcp.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            return completed == task && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static byte[]? ParseMac(string mac)
    {
        try
        {
            var clean = mac.Replace(":", "").Replace("-", "").Replace(".", "").Trim();
            if (clean.Length != 12) return null;
            var bytes = new byte[6];
            for (int i = 0; i < 6; i++)
                bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}

