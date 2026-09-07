using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Black_Hole_Cross.ControlDiagnostic
{
    public partial class NetworkControl : UserControl
    {
        private DispatcherTimer _metricsTimer;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastCheckTime = DateTime.Now;
        private long _lastPingMs = 0;
        private string _currentInterfaceName = "";

        private bool _isPingRunning = false;
        private readonly string _pingTarget = "1.1.1.1";
        private DateTime _lastPingUpdateTime = DateTime.MinValue;
        private DateTime _lastIpUpdateTime = DateTime.MinValue;

        public NetworkControl()
        {
            InitializeComponent();
            InitNetworkMonitor();
            LoadNetworkInterfaces();
            LoadStaticData();
        }

        private void InitNetworkMonitor()
        {
            _metricsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _metricsTimer.Tick += async (s, e) => await UpdateDynamicMetricsAsync();
            _metricsTimer.Start();
        }

      
        private void LoadNetworkInterfaces()
        {
            try
            {
                var interfaceList = new List<string>();
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                   
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        interfaceList.Add($"{ni.Name} ({ni.NetworkInterfaceType})");
                    }
                }

                NetworkCombo.ItemsSource = interfaceList;
                if (interfaceList.Count > 0)
                    NetworkCombo.SelectedIndex = 0;
            }
            catch { }
        }

      
        private void LoadStaticData()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                    {
                        _currentInterfaceName = ni.Name;

                      
                        var address = ni.GetPhysicalAddress().ToString();
                        MacText.Text = string.IsNullOrEmpty(address) ? "MAC: N/A" : $"MAC: {address}";

                        var props = ni.GetIPProperties();
                        foreach (var ip in props.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                LocalIpText.Text = ip.Address.ToString();
                                SubnetText.Text = $"Subnet: {ip.IPv4Mask?.ToString() ?? "N/A"}";
                                break;
                            }
                        }

                        foreach (var gw in props.GatewayAddresses)
                        {
                            GatewayText.Text = gw.Address.ToString();
                            break;
                        }

                       
                        try
                        {
                            var ipv4 = props.GetIPv4Properties();
                            if (ipv4 != null)
                            {
                                DhcpStatusText.Text = ipv4.IsDhcpEnabled ? "DHCP: Enabled" : "DHCP: Disabled";
                                DhcpStatusText.Foreground = ipv4.IsDhcpEnabled ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.Orange;
                            }
                        }
                        catch
                        {
                            DhcpStatusText.Text = "DHCP: Auto";
                        }

                        break;
                    }
                }
            }
            catch { }
        }

    
        private async Task UpdateDynamicMetricsAsync()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                    {
                        var stats = ni.GetIPv4Statistics();
                        DateTime now = DateTime.Now;
                        double timeDiff = (now - _lastCheckTime).TotalSeconds;

                        if (timeDiff > 0 && _lastBytesReceived > 0)
                        {
                            double downSpeedKb = ((stats.BytesReceived - _lastBytesReceived) / 1024.0) / timeDiff;
                            double upSpeedKb = ((stats.BytesSent - _lastBytesSent) / 1024.0) / timeDiff;

                            DownSpeedText.Text = $"{downSpeedKb:0.0} KB/s";
                            UpSpeedText.Text = $"{upSpeedKb:0.0} KB/s";

                            DownProgress.Value = Math.Min(downSpeedKb, DownProgress.Maximum);
                            UpProgress.Value = Math.Min(upSpeedKb, UpProgress.Maximum);

                            TotalDownText.Text = $"↓ {stats.BytesReceived / (1024 * 1024):F1} MB";
                            TotalUpText.Text = $"↑ {stats.BytesSent / (1024 * 1024):F1} MB";
                        }

                        _lastBytesReceived = stats.BytesReceived;
                        _lastBytesSent = stats.BytesSent;
                        _lastCheckTime = now;
                        break;
                    }
                }
            }
            catch { }

            if ((DateTime.Now - _lastPingUpdateTime).TotalSeconds >= 5)
            {
                _lastPingUpdateTime = DateTime.Now;
                _ = Task.Run(async () => await UpdatePingMetrics());
            }

            if (PublicIpText.Text == "Checking..." || PublicIpText.Text == "Offline" ||
                (DateTime.Now - _lastIpUpdateTime).TotalMinutes >= 2)
            {
                _lastIpUpdateTime = DateTime.Now;
                _ = Task.Run(async () => await UpdatePublicIp());
            }

            UpdateUptime();
        }

        private async Task UpdatePingMetrics()
        {
            if (_isPingRunning) return;
            _isPingRunning = true;

            try
            {
                using var ping = new Ping();
                int packetsSent = 3;
                int packetsReceived = 0;
                long totalPing = 0;

                for (int i = 0; i < packetsSent; i++)
                {
                    var reply = await ping.SendPingAsync(_pingTarget, 800);
                    if (reply.Status == IPStatus.Success)
                    {
                        packetsReceived++;
                        totalPing += reply.RoundtripTime;
                    }
                    await Task.Delay(150);
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (packetsReceived > 0)
                    {
                        long avgPing = totalPing / packetsReceived;
                        float packetLoss = ((packetsSent - packetsReceived) / (float)packetsSent) * 100;
                        long jitter = Math.Abs(avgPing - _lastPingMs);
                        _lastPingMs = avgPing;

                        PingText.Text = $"{avgPing} ms";
                        JitterText.Text = $"Jitter: {jitter} ms";
                        PacketLossText.Text = $"Loss: {packetLoss:F1}%";

                        if (avgPing < 30) PingText.Foreground = Avalonia.Media.Brushes.LightGreen;
                        else if (avgPing < 80) PingText.Foreground = Avalonia.Media.Brushes.Yellow;
                        else PingText.Foreground = Avalonia.Media.Brushes.OrangeRed;
                    }
                    else
                    {
                        PingText.Text = "Timeout";
                        PacketLossText.Text = "Loss: 100%";
                    }
                });
            }
            catch { }
            finally
            {
                _isPingRunning = false;
            }
        }

        private async Task UpdatePublicIp()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                string ip = (await client.GetStringAsync("https://api.ipify.org")).Trim();

                await Dispatcher.UIThread.InvokeAsync(() => PublicIpText.Text = ip);
                await UpdateIpLocation(ip);
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PublicIpText.Text = "Offline";
                    IpLocationText.Text = "📍 Offline";
                });
            }
        }

        private async Task UpdateIpLocation(string ip)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await client.GetStringAsync($"http://ip-api.com/json/{ip}?fields=country,city,isp");
                using var data = JsonDocument.Parse(response);
                var root = data.RootElement;

                string country = root.TryGetProperty("country", out var c) ? c.GetString() ?? "Unknown" : "Unknown";
                string city = root.TryGetProperty("city", out var ct) ? ct.GetString() ?? "Unknown" : "Unknown";
                string isp = root.TryGetProperty("isp", out var i) ? i.GetString() ?? "Unknown" : "Unknown";

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IpLocationText.Text = $"📍 {city}, {country}";
                    IspText.Text = $"ISP: {isp}";
                });
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() => IpLocationText.Text = "📍 Location N/A");
            }
        }

        private void UpdateUptime()
        {
            try
            {
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                UptimeText.Text = $"⏱ {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            }
            catch { }
        }

       
        private void OnFlushDnsClick(object? sender, RoutedEventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExecuteCommand("cmd.exe", "/c ipconfig /flushdns && netsh winsock reset");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ExecuteCommand("systemd-resolve", "--flush-caches");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ExecuteCommand("dscacheutil", "-flushcache");
            }

            ActionLogText.Text = "✅ Кэш DNS очищен!";
            ActionLogText.Foreground = Avalonia.Media.Brushes.MediumSpringGreen;
            StatusBadge.Text = "● DNS FLUSHED";
        }

        private void OnFullRepairClick(object? sender, RoutedEventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExecuteCommand("cmd.exe", "/c ipconfig /release && ipconfig /renew");
            }
            else
            {
                ActionLogText.Text = "ℹ️ Авто-сброс адаптера поддерживается на Windows.";
            }

            ActionLogText.Text = "⚡ Сброс адаптера запущен!";
            ActionLogText.Foreground = Avalonia.Media.Brushes.Yellow;
            StatusBadge.Text = "● ADAPTER RESET";
        }

        private void OnTcpTuningClick(object? sender, RoutedEventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExecuteCommand("cmd.exe", "/c netsh int tcp set global autotuninglevel=normal");
                ActionLogText.Text = "⚙️ TCP оптимизирован под Windows!";
            }
            else
            {
                ActionLogText.Text = "⚙️ TCP стеки на Unix/Linux оптимизированы ядром.";
            }

            ActionLogText.Foreground = Avalonia.Media.Brushes.Magenta;
            StatusBadge.Text = "● TCP TUNED";
        }

        private void OnDnsChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DnsCombo == null || string.IsNullOrEmpty(_currentInterfaceName)) return;

            string dnsIp = DnsCombo.SelectedIndex switch
            {
                1 => "1.1.1.1",
                2 => "8.8.8.8",
                3 => "94.140.14.14",
                4 => "9.9.9.9",
                5 => "208.67.222.222",
                _ => "dhcp"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (dnsIp == "dhcp")
                    ExecuteCommand("cmd.exe", $"/c netsh interface ip set dns name=\"{_currentInterfaceName}\" dhcp");
                else
                    ExecuteCommand("cmd.exe", $"/c netsh interface ip set dns name=\"{_currentInterfaceName}\" static {dnsIp}");
            }

            ActionLogText.Text = $"🌐 DNS изменен: {dnsIp}";
            ActionLogText.Foreground = Avalonia.Media.Brushes.Cyan;
        }

        private void OnNetworkInterfaceChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (NetworkCombo.SelectedItem != null)
            {
                string selected = NetworkCombo.SelectedItem.ToString() ?? "";
                ActionLogText.Text = $"🔄 Выбран адаптер: {selected}";
                ActionLogText.Foreground = Avalonia.Media.Brushes.LightBlue;
                LoadStaticData();
            }
        }

        private static void ExecuteCommand(string fileName, string args)
        {
            Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(fileName, args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                }
                catch { }
            });
        }
    }
}
