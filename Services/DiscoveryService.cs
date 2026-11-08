using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SamsungMiniRemote.Models;

namespace SamsungMiniRemote.Services;

/// <summary>
/// Discovers Samsung Smart TVs on the local network using SSDP and a fallback
/// HTTP probe on the known Samsung REST API endpoint.
/// </summary>
public sealed class DiscoveryService
{
    private const int SsdpPort = 1900;
    private const string SsdpMulticast = "239.255.255.250";

    // Samsung TVs respond to the dial-multiscreen SSDP service type.
    private static readonly string SsdpSearchMessage =
        "M-SEARCH * HTTP/1.1\r\n" +
        $"HOST: {SsdpMulticast}:{SsdpPort}\r\n" +
        "MAN: \"ssdp:discover\"\r\n" +
        "MX: 3\r\n" +
        "ST: urn:dial-multiscreen-org:service:dial:1\r\n\r\n";

    /// <summary>
    /// Discovers Samsung TVs using SSDP multicast, then falls back to a
    /// subnet scan probing port 8001/8002 for the Samsung device info REST endpoint.
    /// </summary>
    public async Task<List<SamsungTvDevice>> DiscoverAsync(CancellationToken ct = default)
    {
        var found = new Dictionary<string, SamsungTvDevice>(StringComparer.OrdinalIgnoreCase);

        // Phase 1: SSDP
        try
        {
            var ssdpResults = await SsdpDiscoverAsync(ct);
            foreach (var d in ssdpResults)
                found.TryAdd(d.IpAddress, d);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* SSDP may be blocked; continue to fallback */ }

        // Phase 2: HTTP probe on local subnet (only if SSDP found nothing or as supplement)
        try
        {
            var httpResults = await SubnetProbeAsync(ct);
            foreach (var d in httpResults)
                found.TryAdd(d.IpAddress, d);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Subnet scan failure; use whatever we have */ }

        return found.Values.ToList();
    }

    private async Task<List<SamsungTvDevice>> SsdpDiscoverAsync(CancellationToken ct)
    {
        var results = new List<SamsungTvDevice>();
        using var client = new UdpClient();
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        var data = Encoding.UTF8.GetBytes(SsdpSearchMessage);
        var target = new IPEndPoint(IPAddress.Parse(SsdpMulticast), SsdpPort);

        // Send the search twice to improve reliability.
        await client.SendAsync(data, data.Length, target);
        await Task.Delay(200, ct);
        await client.SendAsync(data, data.Length, target);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (client.Available > 0)
            {
                var result = await client.ReceiveAsync(ct);
                var response = Encoding.UTF8.GetString(result.Buffer);
                var ip = result.RemoteEndPoint.Address.ToString();

                // Only process responses that look like Samsung TVs.
                if (response.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("dial", StringComparison.OrdinalIgnoreCase))
                {
                    var device = await ProbeDeviceInfoAsync(ip, ct);
                    if (device != null)
                        results.Add(device);
                }
            }
            else
            {
                await Task.Delay(100, ct);
            }
        }

        return results;
    }

    private async Task<List<SamsungTvDevice>> SubnetProbeAsync(CancellationToken ct)
    {
        var results = new List<SamsungTvDevice>();
        var localIps = GetLocalIpAddresses();

        foreach (var localIp in localIps)
        {
            var parts = localIp.Split('.');
            if (parts.Length != 4) continue;

            var prefix = $"{parts[0]}.{parts[1]}.{parts[2]}";
            var tasks = new List<Task<SamsungTvDevice?>>();

            for (int i = 1; i <= 254; i++)
            {
                var ip = $"{prefix}.{i}";
                if (ip == localIp) continue;
                tasks.Add(ProbeDeviceInfoAsync(ip, ct, timeoutMs: 800));
            }

            // Run up to 50 concurrent probes.
            var semaphore = new SemaphoreSlim(50);
            var throttled = tasks.Select(async t =>
            {
                await semaphore.WaitAsync(ct);
                try { return await t; }
                finally { semaphore.Release(); }
            });

            var probeResults = await Task.WhenAll(tasks);
            foreach (var d in probeResults)
            {
                if (d != null)
                    results.Add(d);
            }
        }

        return results;
    }

    /// <summary>
    /// Probes a Samsung TV's REST info endpoint to get device name, model, and ID.
    /// Tries port 8002 (TLS) first, then port 8001 (plain).
    /// </summary>
    public async Task<SamsungTvDevice?> ProbeDeviceInfoAsync(string ip, CancellationToken ct, int timeoutMs = 2000)
    {
        foreach (var port in new[] { 8002, 8001 })
        {
            try
            {
                var scheme = port == 8002 ? "https" : "http";
                var url = $"{scheme}://{ip}:{port}/api/v2/";

                using var handler = new System.Net.Http.HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                using var http = new System.Net.Http.HttpClient(handler)
                {
                    Timeout = TimeSpan.FromMilliseconds(timeoutMs)
                };

                var response = await http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var device = root.TryGetProperty("device", out var dev) ? dev : root;

                return new SamsungTvDevice
                {
                    Id = device.TryGetProperty("id", out var id) ? id.GetString() ?? ip : ip,
                    Name = device.TryGetProperty("name", out var name) ? name.GetString() ?? "" :
                           device.TryGetProperty("ModelName", out var mn) ? mn.GetString() ?? "" : "",
                    Model = device.TryGetProperty("modelName", out var model) ? model.GetString() ?? "" :
                            device.TryGetProperty("ModelName", out var mn2) ? mn2.GetString() ?? "" : "",
                    IpAddress = ip,
                    Port = port,
                    MacAddress = device.TryGetProperty("wifiMac", out var mac) ? mac.GetString() :
                                 device.TryGetProperty("mac", out var mac2) ? mac2.GetString() : null
                };
            }
            catch { /* This port didn't work; try next */ }
        }

        return null;
    }

    private static List<string> GetLocalIpAddresses()
    {
        var ips = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        ips.Add(addr.Address.ToString());
                }
            }
        }
        catch { }
        return ips;
    }
}

