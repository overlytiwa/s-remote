using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SamsungMiniRemote.Models;

namespace SamsungMiniRemote.Services;

/// <summary>
/// Connection states for the Samsung TV WebSocket link.
/// </summary>
public enum TvConnectionState
{
    Disconnected,
    Connecting,
    AwaitingAuth,
    Connected
}

/// <summary>
/// Manages the WebSocket connection to a Samsung Smart TV's remote-control API.
/// Handles authentication, token management, command sending, and reconnection.
/// </summary>
public sealed class SamsungRemoteService : IDisposable
{
    private ClientWebSocket? _ws;
    private readonly SettingsService _settings;
    private SamsungTvDevice? _currentDevice;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private string? _pendingToken;
    private bool _disposed;

    private const string AppName = "S-Remote";
    // Base64-encoded app name used in the Samsung WS URL.
    private static readonly string AppNameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(AppName));

    public TvConnectionState State { get; private set; } = TvConnectionState.Disconnected;

    public event Action<TvConnectionState>? ConnectionStateChanged;
    public event Action<string>? TokenReceived;
    public event Action<string>? ErrorOccurred;

    public SamsungTvDevice? CurrentDevice => _currentDevice;

    public SamsungRemoteService(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Connects to the specified Samsung TV. If a token is saved, it is reused.
    /// Otherwise, a pairing request is initiated (the TV will show a permission prompt).
    /// </summary>
    public async Task ConnectAsync(SamsungTvDevice device, CancellationToken ct = default)
    {
        await DisconnectAsync();

        _currentDevice = device;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        SetState(TvConnectionState.Connecting);

        try
        {
            var token = _settings.GetToken(device.IpAddress);
            var uri = BuildUri(device, token);

            _ws = new ClientWebSocket();
            _ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            connectCts.CancelAfter(TimeSpan.FromSeconds(8));

            await _ws.ConnectAsync(uri, connectCts.Token);

            // If no token was available, we're in pairing mode.
            if (string.IsNullOrEmpty(token))
                SetState(TvConnectionState.AwaitingAuth);

            // Start background receive loop to handle auth responses.
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            // Wait briefly for the initial response that confirms connection or provides token.
            await Task.Delay(1500, _cts.Token);

            // If we already had a token and connection is still open, consider it connected.
            if (!string.IsNullOrEmpty(token) && _ws.State == WebSocketState.Open)
                SetState(TvConnectionState.Connected);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SetState(TvConnectionState.Disconnected);
            throw;
        }
        catch (Exception ex)
        {
            SetState(TvConnectionState.Disconnected);
            ErrorOccurred?.Invoke($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a remote-control key command to the connected TV.
    /// </summary>
    public async Task SendKeyAsync(string keyCode)
    {
        if (_ws == null || _ws.State != WebSocketState.Open || State != TvConnectionState.Connected)
            return;

        var payload = new
        {
            method = "ms.remote.control",
            @params = new
            {
                Cmd = "Click",
                DataOfCmd = keyCode,
                Option = "false",
                TypeOfRemote = "SendRemoteKey"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                _cts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Send failed: {ex.Message}");
            SetState(TvConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Gracefully disconnects from the TV.
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }

        if (_ws != null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeCts.Token);
                }
            }
            catch { }
            _ws.Dispose();
            _ws = null;
        }

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
            _receiveTask = null;
        }

        _cts?.Dispose();
        _cts = null;
        _currentDevice = null;

        SetState(TvConnectionState.Disconnected);
    }

    /// <summary>
    /// Whether the service is currently connected and able to send commands.
    /// </summary>
    public bool IsConnected => _ws?.State == WebSocketState.Open && State == TvConnectionState.Connected;

    private Uri BuildUri(SamsungTvDevice device, string? token)
    {
        var scheme = device.Port == 8002 ? "wss" : "ws";
        var url = $"{scheme}://{device.IpAddress}:{device.Port}/api/v2/channels/samsung.remote.control?name={AppNameB64}";
        if (!string.IsNullOrEmpty(token))
            url += $"&token={token}";
        return new Uri(url);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    SetState(TvConnectionState.Disconnected);
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleMessage(message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException)
        {
            SetState(TvConnectionState.Disconnected);
        }
        catch { }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("event", out var evt))
            {
                var eventName = evt.GetString() ?? "";

                switch (eventName)
                {
                    case "ms.channel.connect":
                        // Connection confirmed. Extract token if present.
                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("token", out var tokenProp))
                        {
                            var token = tokenProp.GetString();
                            if (!string.IsNullOrEmpty(token) && _currentDevice != null)
                            {
                                _settings.SetToken(_currentDevice.IpAddress, token);
                                _pendingToken = token;
                                TokenReceived?.Invoke(token);
                            }
                        }
                        SetState(TvConnectionState.Connected);
                        break;

                    case "ms.channel.clientConnect":
                        // Another client connected; ignore.
                        break;

                    case "ms.channel.clientDisconnect":
                        break;

                    case "ms.error":
                        // Access denied or token expired.
                        if (_currentDevice != null)
                        {
                            _settings.RemoveToken(_currentDevice.IpAddress);
                        }
                        ErrorOccurred?.Invoke("TV denied the connection. Please allow access on the TV.");
                        SetState(TvConnectionState.Disconnected);
                        break;
                }
            }
        }
        catch { }
    }

    private void SetState(TvConnectionState newState)
    {
        if (State == newState) return;
        State = newState;
        ConnectionStateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts?.Cancel(); } catch { }
        try { _ws?.Dispose(); } catch { }
        _cts?.Dispose();
    }
}

