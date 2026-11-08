using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SamsungMiniRemote.Controls;
using SamsungMiniRemote.Models;
using SamsungMiniRemote.Services;

// Resolve WPF vs System.Drawing / WinForms ambiguities explicitly.
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace SamsungMiniRemote;

public partial class MainWindow : Window
{
    private SamsungRemoteService? _remote;
    private DiscoveryService? _discovery;
    private SettingsService? _settings;

    private bool _isMuted;
    private bool _isPlaying;
    private DispatcherTimer? _volRepeatTimer;
    private string? _volRepeatKey;
    private CancellationTokenSource? _discoveryCts;

    // Track discovered devices for the popup list.
    private List<SamsungTvDevice> _discoveredTvs = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    // ───────────────────── Window lifecycle ─────────────────────

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position near bottom-right of usable desktop area.
        var area = SystemParameters.WorkArea;
        const double edge = 14;
        Left = Math.Max(area.Left, area.Right - ActualWidth - edge);
        Top = Math.Max(area.Top, area.Bottom - ActualHeight - edge);

        // Initialize services.
        _settings = SettingsService.Load();
        _discovery = new DiscoveryService();
        _remote = new SamsungRemoteService(_settings);
        _remote.ConnectionStateChanged += OnConnectionStateChanged;
        _remote.ErrorOccurred += OnRemoteError;

        // Set initial UI state.
        UpdateControlsEnabled(false);

        // Auto-reconnect to last TV if available.
        if (!string.IsNullOrEmpty(_settings.LastTvIp))
        {
            var lastTv = new SamsungTvDevice
            {
                IpAddress = _settings.LastTvIp,
                Name = _settings.LastTvName ?? "",
                Model = _settings.LastTvModel ?? "",
                Port = _settings.LastTvPort
            };

            Dispatcher.Invoke(() =>
            {
                TvPickerText.Text = lastTv.DisplayName;
                UpdateStatusDot(TvConnectionState.Connecting);
            });

            await Task.Run(async () =>
            {
                try
                {
                    // Quick probe to see if TV is available.
                    if (await WakeOnLan.IsReachableAsync(lastTv.IpAddress, lastTv.Port, 2500))
                    {
                        // Re-probe for full device info.
                        var freshInfo = await _discovery!.ProbeDeviceInfoAsync(lastTv.IpAddress, CancellationToken.None, 3000);
                        if (freshInfo != null)
                        {
                            lastTv.Name = freshInfo.Name;
                            lastTv.Model = freshInfo.Model;
                            lastTv.MacAddress = freshInfo.MacAddress;
                        }
                        await _remote!.ConnectAsync(lastTv);
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TvPickerText.Text = $"{lastTv.DisplayName} (offline)";
                            UpdateStatusDot(TvConnectionState.Disconnected);
                        });
                    }
                }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        TvPickerText.Text = "Choose a TV";
                        UpdateStatusDot(TvConnectionState.Disconnected);
                    });
                }
            });
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Alt+F4 and taskbar close behave exactly like the X button.
        if (System.Windows.Application.Current is App app && !app.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    // ───────────────────── Title bar controls ─────────────────────

    private void DragZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* Drag ended during gesture. */ }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    // ───────────────────── Connection state UI ─────────────────────

    private void OnConnectionStateChanged(TvConnectionState state)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStatusDot(state);

            switch (state)
            {
                case TvConnectionState.Connected:
                    var dev = _remote?.CurrentDevice;
                    TvPickerText.Text = dev?.DisplayName ?? "Connected";
                    UpdateControlsEnabled(true);
                    // Save as last TV.
                    if (dev != null && _settings != null)
                    {
                        _settings.LastTvIp = dev.IpAddress;
                        _settings.LastTvName = dev.Name;
                        _settings.LastTvModel = dev.Model;
                        _settings.LastTvPort = dev.Port;
                        _settings.SetMac(dev.IpAddress, dev.MacAddress);
                        _settings.Save();
                    }
                    break;

                case TvConnectionState.Connecting:
                    TvPickerText.Text = "Connecting…";
                    UpdateControlsEnabled(false);
                    break;

                case TvConnectionState.AwaitingAuth:
                    TvPickerText.Text = "Allow on TV…";
                    UpdateControlsEnabled(false);
                    break;

                case TvConnectionState.Disconnected:
                    if (_settings != null && !string.IsNullOrEmpty(_settings.LastTvName))
                        TvPickerText.Text = $"{_settings.LastTvName} (offline)";
                    else
                        TvPickerText.Text = "Choose a TV";
                    UpdateControlsEnabled(false);
                    break;
            }
        });
    }

    private void OnRemoteError(string message)
    {
        // Subtle: just update the status indicator and picker text.
        Dispatcher.Invoke(() =>
        {
            UpdateStatusDot(TvConnectionState.Disconnected);
        });
    }

    private void UpdateStatusDot(TvConnectionState state)
    {
        StatusDot.Fill = state switch
        {
            TvConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)),   // Green
            TvConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)),   // Yellow
            TvConnectionState.AwaitingAuth => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)), // Blue
            _ => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x58))                                // Gray
        };
    }

    private void UpdateControlsEnabled(bool connected)
    {
        BtnMute.IsEnabled = connected;
        BtnUp.IsEnabled = connected;
        BtnDown.IsEnabled = connected;
        BtnLeft.IsEnabled = connected;
        BtnRight.IsEnabled = connected;
        BtnOk.IsEnabled = connected;
        BtnVolDown.IsEnabled = connected;
        BtnVolUp.IsEnabled = connected;
        BtnPlayPause.IsEnabled = connected;
        BtnBack.IsEnabled = connected;
        BtnHome.IsEnabled = connected;
        // Power button is always enabled (for WoL).
        BtnPower.IsEnabled = true;
    }

    // ───────────────────── TV Picker / Discovery ─────────────────────

    private async void TvPicker_Click(object sender, RoutedEventArgs e)
    {
        TvPopup.IsOpen = !TvPopup.IsOpen;
        if (TvPopup.IsOpen)
        {
            await RunDiscoveryAsync();
        }
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        await RunDiscoveryAsync();
    }

    private async Task RunDiscoveryAsync()
    {
        // Cancel any ongoing discovery.
        _discoveryCts?.Cancel();
        _discoveryCts = new CancellationTokenSource();
        var ct = _discoveryCts.Token;

        TvListPanel.Children.Clear();
        TvSearchStatus.Text = "Searching…";
        TvListPanel.Children.Add(TvSearchStatus);

        try
        {
            _discoveredTvs = await Task.Run(() => _discovery!.DiscoverAsync(ct), ct);

            Dispatcher.Invoke(() =>
            {
                TvListPanel.Children.Clear();
                if (_discoveredTvs.Count == 0)
                {
                    TvSearchStatus.Text = "No TVs found";
                    TvListPanel.Children.Add(TvSearchStatus);
                }
                else
                {
                    foreach (var tv in _discoveredTvs)
                    {
                        var item = CreateTvListItem(tv);
                        TvListPanel.Children.Add(item);
                    }
                }
            });
        }
        catch (OperationCanceledException) { }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                TvListPanel.Children.Clear();
                TvSearchStatus.Text = "Discovery failed";
                TvListPanel.Children.Add(TvSearchStatus);
            });
        }
    }

    private Border CreateTvListItem(SamsungTvDevice tv)
    {
        var nameText = new TextBlock
        {
            Text = tv.DisplayName,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF2))
        };

        var detailText = new TextBlock
        {
            Text = $"{(string.IsNullOrWhiteSpace(tv.Model) ? "" : tv.Model + " · ")}{tv.IpAddress}",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x86)),
            Margin = new Thickness(0, 1, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(nameText);
        stack.Children.Add(detailText);

        var border = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Cursor = Cursors.Hand,
            Child = stack
        };

        border.MouseEnter += (_, _) => border.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E));
        border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
        border.MouseLeftButtonUp += async (_, _) =>
        {
            TvPopup.IsOpen = false;
            await ConnectToTvAsync(tv);
        };

        return border;
    }

    private async Task ConnectToTvAsync(SamsungTvDevice tv)
    {
        if (_remote == null) return;

        TvPickerText.Text = "Connecting…";
        UpdateStatusDot(TvConnectionState.Connecting);

        try
        {
            await _remote.ConnectAsync(tv);
        }
        catch (Exception)
        {
            TvPickerText.Text = "Connection failed";
            UpdateStatusDot(TvConnectionState.Disconnected);
        }
    }

    // ───────────────────── Remote control commands ─────────────────────

    private async void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (_remote == null || !_remote.IsConnected) return;
        await _remote.SendKeyAsync("KEY_MUTE");
        _isMuted = !_isMuted;
        MuteIcon.Kind = _isMuted ? RemoteIcon.VolumeOff : RemoteIcon.VolumeOn;
    }

    private async void Power_Click(object sender, RoutedEventArgs e)
    {
        if (_remote != null && _remote.IsConnected)
        {
            // TV is connected — send power off.
            await _remote.SendKeyAsync("KEY_POWER");
        }
        else if (_settings != null && !string.IsNullOrEmpty(_settings.LastTvIp))
        {
            // TV is offline — attempt Wake-on-LAN.
            var mac = _settings.GetMac(_settings.LastTvIp);
            if (!string.IsNullOrEmpty(mac))
            {
                await WakeOnLan.SendAsync(mac);
                TvPickerText.Text = "WoL sent…";
                UpdateStatusDot(TvConnectionState.Connecting);

                // Try reconnecting after a delay.
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    if (await WakeOnLan.IsReachableAsync(_settings.LastTvIp, _settings.LastTvPort))
                    {
                        var tv = new SamsungTvDevice
                        {
                            IpAddress = _settings.LastTvIp,
                            Name = _settings.LastTvName ?? "",
                            Model = _settings.LastTvModel ?? "",
                            Port = _settings.LastTvPort,
                            MacAddress = mac
                        };
                        await _remote!.ConnectAsync(tv);
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TvPickerText.Text = $"{_settings.LastTvName ?? _settings.LastTvIp} (offline)";
                            UpdateStatusDot(TvConnectionState.Disconnected);
                        });
                    }
                });
            }
            else
            {
                // No MAC address saved; can't send WoL.
                TvPickerText.Text = "No WoL MAC saved";
                await Task.Delay(2000);
                TvPickerText.Text = $"{_settings.LastTvName ?? "Choose a TV"} (offline)";
            }
        }
    }

    // D-pad
    private async void DpadUp_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_UP");
    private async void DpadDown_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_DOWN");
    private async void DpadLeft_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_LEFT");
    private async void DpadRight_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_RIGHT");
    private async void DpadOk_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_ENTER");

    // Volume with repeat-on-hold.
    private bool _volWasRepeated;

    private async void VolDown_Click(object sender, RoutedEventArgs e)
    {
        if (!_volWasRepeated)
            await SendKeyIfConnected("KEY_VOLDOWN");
        _volWasRepeated = false;
    }

    private async void VolUp_Click(object sender, RoutedEventArgs e)
    {
        if (!_volWasRepeated)
            await SendKeyIfConnected("KEY_VOLUP");
        _volWasRepeated = false;
    }

    private void VolDown_MouseDown(object sender, MouseButtonEventArgs e) => StartVolumeRepeat("KEY_VOLDOWN");
    private void VolUp_MouseDown(object sender, MouseButtonEventArgs e) => StartVolumeRepeat("KEY_VOLUP");
    private void VolRepeat_MouseUp(object sender, EventArgs e) => StopVolumeRepeat();

    private void StartVolumeRepeat(string key)
    {
        _volRepeatKey = key;
        _volWasRepeated = false;
        _volRepeatTimer?.Stop();
        _volRepeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _volRepeatTimer.Tick += async (_, _) =>
        {
            if (_volRepeatKey != null)
            {
                _volWasRepeated = true;
                await SendKeyIfConnected(_volRepeatKey);
            }
        };

        // Start repeating after a 350ms hold delay.
        _ = Task.Delay(350).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                // Only start if the button is still held down.
                if (_volRepeatKey != null)
                    _volRepeatTimer?.Start();
            });
        });
    }

    private void StopVolumeRepeat()
    {
        _volRepeatTimer?.Stop();
        _volRepeatTimer = null;
        _volRepeatKey = null;
    }

    // Play/Pause
    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_remote == null || !_remote.IsConnected) return;

        if (_isPlaying)
        {
            await _remote.SendKeyAsync("KEY_PAUSE");
            _isPlaying = false;
            PlayPauseIcon.Kind = RemoteIcon.Play;
        }
        else
        {
            await _remote.SendKeyAsync("KEY_PLAY");
            _isPlaying = true;
            PlayPauseIcon.Kind = RemoteIcon.Pause;
        }
    }

    // Back
    private async void Back_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_RETURN");

    // Home
    private async void Home_Click(object sender, RoutedEventArgs e) => await SendKeyIfConnected("KEY_HOME");

    private async Task SendKeyIfConnected(string keyCode)
    {
        if (_remote != null && _remote.IsConnected)
            await _remote.SendKeyAsync(keyCode);
    }

    // ───────────────────── Cleanup ─────────────────────

    internal async Task CleanupAsync()
    {
        _discoveryCts?.Cancel();
        StopVolumeRepeat();
        if (_remote != null)
        {
            await _remote.DisconnectAsync();
            _remote.Dispose();
        }
    }
}
