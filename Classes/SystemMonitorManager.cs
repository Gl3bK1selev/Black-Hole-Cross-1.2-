using Avalonia.Threading;
using Black_Hole_Cross.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Black_Hole_Cross;

public class RealSystemMonitor
{
    private readonly DispatcherTimer _monitoringTimer;
    private readonly List<SystemNotification> _sentNotifications;
    public event Action<SystemNotification>? OnNotification;

    private static RealSystemMonitor? _instance;
    public static RealSystemMonitor Instance => _instance ??= new RealSystemMonitor();

    
    private readonly ConcurrentDictionary<string, DateTime> _lastNotificationTimes = new();

   
    private readonly ConcurrentDictionary<string, object> _systemCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly object _cacheLock = new();

   
    private DateTime _lastCpuCheckTime = DateTime.MinValue;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private long _lastLinuxTotalIdle = 0;
    private long _lastLinuxTotalNonIdle = 0;

   
    private bool _isMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

   
    private bool? _isNvidiaSmiAvailable = null;
    private bool? _isRadeonAvailable = null;
    private DateTime _lastGpuCheck = DateTime.MinValue;

    public RealSystemMonitor()
    {
        _sentNotifications = new List<SystemNotification>();

        _monitoringTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3) 
        };
        _monitoringTimer.Tick += MonitorAllSystems;
    }

    public void StartMonitoring() => _monitoringTimer.Start();
    public void StopMonitoring() => _monitoringTimer.Stop();

    private void MonitorAllSystems(object? sender, EventArgs e)
    {
       
        Task.Run(() =>
        {
            try
            {
               
                if ((DateTime.Now - _lastCacheUpdate).TotalSeconds > 30)
                {
                    lock (_cacheLock)
                    {
                        UpdateSystemCache();
                        _lastCacheUpdate = DateTime.Now;
                    }
                }

                
                Parallel.Invoke(
                    () => CheckCPU(),
                    () => CheckMemory(),
                    () => CheckDiskActivity(),
                    () => CheckNetwork(),
                    () => CheckBattery(),
                    () => CheckProcessProblems(),
                    () => CheckSystemUptime(),
                    () => CheckTemperature(),
                    () => CheckGPUUsage(),
                    () => CheckSystemLoad(),
                    () => CheckOpenPorts(),
                    () => CheckRunningServices()
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка в MonitorAllSystems: {ex.Message}");
            }
        });
    }

    #region Обновление системного кэша

    private void UpdateSystemCache()
    {
        try
        {
            // Информация о системе
            _systemCache["OS"] = RuntimeInformation.OSDescription;
            _systemCache["OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString();
            _systemCache["Processors"] = Environment.ProcessorCount;
            _systemCache["Framework"] = RuntimeInformation.FrameworkDescription;

            // Информация о дисках
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new
                {
                    Name = d.Name,
                    TotalSize = d.TotalSize,
                    AvailableFreeSpace = d.AvailableFreeSpace,
                    DriveType = d.DriveType.ToString(),
                    VolumeLabel = d.VolumeLabel
                })
                .ToList();
            _systemCache["Drives"] = drives;

            // Информация о сети
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Select(n => new
                {
                    Name = n.Name,
                    Speed = n.Speed,
                    Description = n.Description,
                    NetworkInterfaceType = n.NetworkInterfaceType.ToString()
                })
                .ToList();
            _systemCache["NetworkInterfaces"] = networkInterfaces;

            // Информация о памяти (базовая)
            if (_isWindows)
            {
                var memoryStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memoryStatus))
                {
                    _systemCache["TotalPhysicalMemory"] = memoryStatus.ullTotalPhys;
                    _systemCache["AvailablePhysicalMemory"] = memoryStatus.ullAvailPhys;
                }
            }
            else if (_isLinux)
            {
                var memInfo = GetLinuxMemoryInfo();
                _systemCache["TotalPhysicalMemory"] = memInfo.Total;
                _systemCache["AvailablePhysicalMemory"] = memInfo.Available;
            }
            else if (_isMacOs)
            {
                var memInfo = GetMacMemoryInfo();
                _systemCache["TotalPhysicalMemory"] = memInfo.Total;
                _systemCache["AvailablePhysicalMemory"] = memInfo.Available;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обновления кэша: {ex.Message}");
        }
    }

    #endregion

    #region CPU Monitoring

    private void CheckCPU()
    {
        try
        {
            float cpuUsage = GetCrossPlatformCpuUsage();
            if (cpuUsage < 0) return;

            var notificationKey = "CPU";
            if (cpuUsage > 95 && CanSendNotification($"{notificationKey}_Critical", TimeSpan.FromMinutes(1)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = LocalizationService.Instance["CriticalCPUTitle"] ?? "Критическая загрузка ЦП",
                    Message = string.Format(LocalizationService.Instance["CriticalCPUMessage"] ?? "Загрузка процессора: {0}%", cpuUsage.ToString("F1")),
                    Icon = "🔥",
                    Type = NotificationType.Critical
                });
            }
            else if (cpuUsage > 85 && CanSendNotification($"{notificationKey}_Warning", TimeSpan.FromMinutes(3)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = LocalizationService.Instance["HighCPUTitle"] ?? "Высокая загрузка ЦП",
                    Message = string.Format(LocalizationService.Instance["HighCPUMessage"] ?? "Загрузка процессора: {0}%", cpuUsage.ToString("F1")),
                    Icon = "⚡",
                    Type = NotificationType.Warning
                });
            }
            else if (cpuUsage < 10 && CanSendNotification($"{notificationKey}_Low", TimeSpan.FromMinutes(5)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = "Низкая загрузка ЦП",
                    Message = string.Format("Загрузка процессора: {0}% - система простаивает", cpuUsage.ToString("F1")),
                    Icon = "💤",
                    Type = NotificationType.Info
                });
            }

          
            _systemCache["CurrentCPU"] = cpuUsage;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга CPU: {ex.Message}");
        }
    }

    private float GetCrossPlatformCpuUsage()
    {
        try
        {
            if (_isWindows)
            {
                return GetWindowsCpuUsage();
            }
            else if (_isLinux)
            {
                return GetLinuxCpuUsage();
            }
            else if (_isMacOs)
            {
                return GetMacCpuUsage();
            }
        }
        catch { }

        return -1;
    }

    private float GetWindowsCpuUsage()
    {
        try
        {
          
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-Counter '\\Processor(_Total)\\% Processor Time' | Select-Object -ExpandProperty CounterSamples | Select-Object -ExpandProperty CookedValue\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(2000);

            if (float.TryParse(output, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float cpu))
            {
                return cpu;
            }
        }
        catch { }

       
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "cpu get loadpercentage /format:csv",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1000);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int load))
                {
                    return load;
                }
            }
        }
        catch { }

        return -1;
    }

    private float GetLinuxCpuUsage()
    {
        try
        {
            if (!File.Exists("/proc/stat")) return -1;

            var lines = File.ReadLines("/proc/stat").Take(1).ToList();
            if (lines.Count == 0) return -1;

            var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return -1;

            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
            long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
            long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;
            long steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

            long nonIdle = user + nice + system + irq + softirq + steal;
            long total = nonIdle + idle + iowait;

            if (_lastLinuxTotalIdle == 0 && _lastLinuxTotalNonIdle == 0)
            {
                _lastLinuxTotalIdle = idle + iowait;
                _lastLinuxTotalNonIdle = nonIdle;
                return -1;
            }

            long diffIdle = (idle + iowait) - _lastLinuxTotalIdle;
            long diffNonIdle = nonIdle - _lastLinuxTotalNonIdle;
            long diffTotal = diffIdle + diffNonIdle;

            _lastLinuxTotalIdle = idle + iowait;
            _lastLinuxTotalNonIdle = nonIdle;

            if (diffTotal == 0) return 0;
            return (float)(diffNonIdle * 100.0 / diffTotal);
        }
        catch
        {
            return -1;
        }
    }

    private float GetMacCpuUsage()
    {
        try
        {
           
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "top",
                    Arguments = "-l1 -n0",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(500);

            var match = Regex.Match(output, @"CPU usage: ([\d.]+)% user, ([\d.]+)% sys, ([\d.]+)% idle");
            if (match.Success)
            {
                float user = float.Parse(match.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                float sys = float.Parse(match.Groups[2].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                return user + sys;
            }

           
            using var p2 = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "vm.loadavg",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p2.Start();
            string output2 = p2.StandardOutput.ReadToEnd();
            p2.WaitForExit(500);

            var match2 = Regex.Match(output2, @"\{ ([\d.]+) ([\d.]+) ([\d.]+) \}");
            if (match2.Success)
            {
                float load1 = float.Parse(match2.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                int cores = Environment.ProcessorCount;
                float cpuPercent = Math.Min(100f, (load1 / cores) * 100f);
                return cpuPercent;
            }

            return -1;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Mac CPU ошибка: {ex.Message}");
            return -1;
        }
    }
    #endregion

    #region Memory Monitoring

    private void CheckMemory()
    {
        try
        {
            float ramUsage = GetCrossPlatformRamUsage();
            if (ramUsage < 0) return;

            var notificationKey = "RAM";
            if (ramUsage > 95 && CanSendNotification($"{notificationKey}_Critical", TimeSpan.FromMinutes(2)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = LocalizationService.Instance["CriticalMemoryTitle"] ?? "Критический расход ОЗУ",
                    Message = string.Format(LocalizationService.Instance["CriticalMemoryMessage"] ?? "Использование памяти: {0}%", ramUsage.ToString("F1")),
                    Icon = "💥",
                    Type = NotificationType.Critical
                });
            }
            else if (ramUsage > 80 && CanSendNotification($"{notificationKey}_Warning", TimeSpan.FromMinutes(5)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = "Высокий расход ОЗУ",
                    Message = string.Format("Использование памяти: {0}%", ramUsage.ToString("F1")),
                    Icon = "⚠️",
                    Type = NotificationType.Warning
                });
            }

            _systemCache["CurrentRAM"] = ramUsage;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга памяти: {ex.Message}");
        }
    }

    private float GetCrossPlatformRamUsage()
    {
        try
        {
            if (_isWindows)
            {
                var memoryStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memoryStatus))
                {
                    long total = (long)memoryStatus.ullTotalPhys;
                    long available = (long)memoryStatus.ullAvailPhys;
                    if (total > 0)
                    {
                        return (float)((total - available) * 100.0 / total);
                    }
                }
            }
            else if (_isLinux)
            {
                var memInfo = GetLinuxMemoryInfo();
                if (memInfo.Total > 0)
                {
                    return (float)((memInfo.Total - memInfo.Available) * 100.0 / memInfo.Total);
                }
            }
            else if (_isMacOs)
            {
                var memInfo = GetMacMemoryInfo();
                if (memInfo.Total > 0)
                {
                    return (float)((memInfo.Total - memInfo.Available) * 100.0 / memInfo.Total);
                }
            }
        }
        catch { }

        return -1;
    }

    private (long Total, long Available, long Used) GetLinuxMemoryInfo()
    {
        try
        {
            if (!File.Exists("/proc/meminfo")) return (0, 0, 0);

            long total = 0, available = 0, free = 0, buffers = 0, cached = 0;
            var lines = File.ReadLines("/proc/meminfo");
            foreach (var line in lines)
            {
                var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Replace("kB", "").Trim();

                if (key == "MemTotal" && long.TryParse(value, out long t)) total = t;
                else if (key == "MemAvailable" && long.TryParse(value, out long a)) available = a;
                else if (key == "MemFree" && long.TryParse(value, out long f)) free = f;
                else if (key == "Buffers" && long.TryParse(value, out long b)) buffers = b;
                else if (key == "Cached" && long.TryParse(value, out long c)) cached = c;
            }

           
            if (available == 0 && total > 0)
            {
                available = free + buffers + cached;
            }

            return (total * 1024, available * 1024, (total - available) * 1024);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private (long Total, long Available, long Used) GetMacMemoryInfo()
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vm_stat",
                    Arguments = "",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(500);

           
            var totalMatch = Regex.Match(output, @"Pages free:\s*(\d+)");
            var activeMatch = Regex.Match(output, @"Pages active:\s*(\d+)");
            var inactiveMatch = Regex.Match(output, @"Pages inactive:\s*(\d+)");
            var speculativeMatch = Regex.Match(output, @"Pages speculative:\s*(\d+)");
            var wiredMatch = Regex.Match(output, @"Pages wired down:\s*(\d+)");
            var occupiedMatch = Regex.Match(output, @"Pages occupied by compressor:\s*(\d+)");

            if (!totalMatch.Success || !activeMatch.Success) return (0, 0, 0);

            long pageSize = 4096; 
            long free = long.Parse(totalMatch.Groups[1].Value);
            long active = long.Parse(activeMatch.Groups[1].Value);
            long inactive = long.Parse(inactiveMatch.Groups[1].Value);
            long speculative = speculativeMatch.Success ? long.Parse(speculativeMatch.Groups[1].Value) : 0;
            long wired = wiredMatch.Success ? long.Parse(wiredMatch.Groups[1].Value) : 0;
            long occupied = occupiedMatch.Success ? long.Parse(occupiedMatch.Groups[1].Value) : 0;

            long total = active + inactive + speculative + wired + free + occupied;
            long available = free + speculative + inactive;

            return (total * pageSize, available * pageSize, (total - available) * pageSize);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    #endregion

    #region Disk Monitoring

    private void CheckDiskActivity()
    {
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                    continue;

                double freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024 / 1024;
                double totalSpaceGB = drive.TotalSize / 1024.0 / 1024 / 1024;
                double freePercent = totalSpaceGB > 0 ? (freeSpaceGB / totalSpaceGB) * 100 : 0;

                string notificationKey = $"Disk_{drive.Name.Replace(":", "")}";

                if (freePercent < 3 && CanSendNotification($"{notificationKey}_Critical", TimeSpan.FromMinutes(10)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = LocalizationService.Instance["CriticalDiskSpaceTitle"] ?? "Критически мало места на диске",
                        Message = string.Format(LocalizationService.Instance["CriticalDiskSpaceMessage"] ?? "На диске {0} осталось {1} GB ({2}%)",
                            drive.Name, freeSpaceGB.ToString("F1"), freePercent.ToString("F1")),
                        Icon = "💾",
                        Type = NotificationType.Critical
                    });
                }
                else if (freePercent < 10 && CanSendNotification($"{notificationKey}_Warning", TimeSpan.FromHours(1)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Мало места на диске",
                        Message = string.Format("На диске {0} осталось {1} GB ({2}%)",
                            drive.Name, freeSpaceGB.ToString("F1"), freePercent.ToString("F1")),
                        Icon = "📀",
                        Type = NotificationType.Warning
                    });
                }

               
                _systemCache[$"Disk_{drive.Name.Replace(":", "")}_Free"] = freeSpaceGB;
                _systemCache[$"Disk_{drive.Name.Replace(":", "")}_Total"] = totalSpaceGB;
                _systemCache[$"Disk_{drive.Name.Replace(":", "")}_Percent"] = freePercent;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга диска: {ex.Message}");
        }
    }

    #endregion

    #region Network Monitoring

   
    private DateTime _lastInternetCheck = DateTime.MinValue;
    private bool _lastInternetStatus = false;
    private readonly object _internetLock = new object();

   
    private DateTime _lastTrafficCheck = DateTime.MinValue;
    private long _lastBytesReceived = 0;
    private long _lastBytesSent = 0;
    private DateTime _lastTrafficSampleTime = DateTime.MinValue;

    
    private DateTime _lastSpeedTest = DateTime.MinValue;
    private double _lastDownloadSpeedMbps = 0;
    private double _lastUploadSpeedMbps = 0;
    private bool _isSpeedTestRunning = false;

   
    private void CheckNetwork()
    {
        try
        {
           
            bool hasNetworkInterface = NetworkInterface.GetIsNetworkAvailable();

           
            bool hasInternet = CheckInternetAccess();

           
            _systemCache["NetworkAvailable"] = hasNetworkInterface;
            _systemCache["InternetAvailable"] = hasInternet;
            _systemCache["NetworkConnected"] = hasInternet || hasNetworkInterface;

           
            Debug.WriteLine($"Network: Interface={hasNetworkInterface}, Internet={hasInternet}");

           
            if (!hasInternet && hasNetworkInterface)
            {
                if (CanSendNotification("Internet_Lost", TimeSpan.FromSeconds(30)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = LocalizationService.Instance["NoInternetTitle"] ?? "Потеряно подключение к интернету",
                        Message = LocalizationService.Instance["NoInternetMessage"] ?? "Сетевой интерфейс активен, но нет доступа в интернет.",
                        Icon = "🌐",
                        Type = NotificationType.Critical
                    });
                }
            }

            
            if (!hasNetworkInterface)
            {
                if (CanSendNotification("Network_Down", TimeSpan.FromMinutes(1)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Нет сетевых подключений",
                        Message = "Все сетевые интерфейсы отключены.",
                        Icon = "📡",
                        Type = NotificationType.Warning
                    });
                }
            }

          
            if (hasInternet && !_lastInternetStatus &&
                CanSendNotification("Internet_Restored", TimeSpan.FromMinutes(5)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = "Подключение к интернету восстановлено",
                    Message = "Доступ в интернет снова доступен.",
                    Icon = "✅",
                    Type = NotificationType.Info
                });
            }

          
            CheckNetworkTraffic();

           
            if (hasInternet)
            {
                CheckInternetSpeed();
            }

           
            CheckNetworkAdapters();

           
            _lastInternetStatus = hasInternet;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга сети: {ex.Message}");
        }
    }

  
    private bool CheckInternetAccess()
    {
       
        lock (_internetLock)
        {
            if ((DateTime.Now - _lastInternetCheck).TotalSeconds < 5)
            {
                return _lastInternetStatus;
            }
            _lastInternetCheck = DateTime.Now;
        }

        try
        {
          
            var pingTasks = new[]
            {
                PingHost("8.8.8.8"),
                PingHost("1.1.1.1"),
                PingHost("google.com"),
                PingHost("ya.ru")
            };

          
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                var task = Task.WhenAny(pingTasks).WaitAsync(cts.Token);
                task.Wait(cts.Token);

                if (task.Result.Result)
                {
                    lock (_internetLock)
                    {
                        _lastInternetStatus = true;
                    }
                    return true;
                }
            }
            catch (TimeoutException)
            {
               
            }
            catch (AggregateException)
            {
                // Ошибки в задачах
            }

          
            if (CheckHttpAccess())
            {
                lock (_internetLock)
                {
                    _lastInternetStatus = true;
                }
                return true;
            }

           
            if (CheckDnsResolution())
            {
                lock (_internetLock)
                {
                    _lastInternetStatus = true;
                }
                return true;
            }

          
            if (CheckSystemNetworkCommand())
            {
                lock (_internetLock)
                {
                    _lastInternetStatus = true;
                }
                return true;
            }

            lock (_internetLock)
            {
                _lastInternetStatus = false;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка проверки интернета: {ex.Message}");
            lock (_internetLock)
            {
                _lastInternetStatus = false;
            }
            return false;
        }
    }

    private async Task<bool> PingHost(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 1000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckHttpAccess()
    {
        try
        {
            var urls = new[]
            {
                "https://www.google.com",
                "https://www.microsoft.com",
                "https://www.cloudflare.com"
            };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            foreach (var url in urls)
            {
                try
                {
                    var response = client.GetAsync(url).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch { }
            }
        }
        catch { }

        return false;
    }

    private bool CheckDnsResolution()
    {
        try
        {
            var hosts = new[] { "google.com", "microsoft.com", "cloudflare.com" };
            foreach (var host in hosts)
            {
                try
                {
                    var entries = System.Net.Dns.GetHostEntry(host);
                    if (entries.AddressList.Length > 0)
                    {
                        return true;
                    }
                }
                catch { }
            }
        }
        catch { }

        return false;
    }

    private bool CheckSystemNetworkCommand()
    {
        try
        {
            // Для Linux/macOS
            if (_isLinux || _isMacOs)
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ping",
                        Arguments = "-c 1 -W 1 8.8.8.8",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);

                return p.ExitCode == 0;
            }
        }
        catch { }

        return false;
    }

 
    private void CheckNetworkTraffic()
    {
        try
        {
            
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                    && n.NetworkInterfaceType != NetworkInterfaceType.Unknown
                    && n.Speed > 0) 
                .ToList();

         
            if (interfaces.Count == 0)
            {
                _lastTrafficSampleTime = DateTime.MinValue;
                return;
            }

            long totalReceived = 0;
            long totalSent = 0;

            foreach (var ni in interfaces)
            {
                try
                {
                    var stats = ni.GetIPStatistics();
                    totalReceived += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                }
                catch { }
            }

            if (_lastTrafficSampleTime != DateTime.MinValue)
            {
                double timeDiff = (DateTime.Now - _lastTrafficSampleTime).TotalSeconds;
                if (timeDiff > 0)
                {
                    long receivedDiff = totalReceived - _lastBytesReceived;
                    long sentDiff = totalSent - _lastBytesSent;

                   
                    if (receivedDiff == 0 && sentDiff == 0)
                    {
                        _lastTrafficSampleTime = DateTime.Now;
                        return;
                    }

                    double downloadSpeedMBps = receivedDiff / timeDiff / 1024.0 / 1024.0; // MB/s
                    double uploadSpeedMBps = sentDiff / timeDiff / 1024.0 / 1024.0; // MB/s

                 
                    _systemCache["CurrentDownloadMBps"] = downloadSpeedMBps;
                    _systemCache["CurrentUploadMBps"] = uploadSpeedMBps;
                    _systemCache["CurrentDownloadMbps"] = downloadSpeedMBps * 8;
                    _systemCache["CurrentUploadMbps"] = uploadSpeedMBps * 8;
                    _systemCache["NetworkTrafficActive"] = (downloadSpeedMBps > 0.01 || uploadSpeedMBps > 0.01);

                    Debug.WriteLine($"Traffic: ↓{downloadSpeedMBps:F2} MB/s, ↑{uploadSpeedMBps:F2} MB/s");
                }
            }
            else
            {
             
                Debug.WriteLine("Network traffic: Initial sample");
            }

            _lastBytesReceived = totalReceived;
            _lastBytesSent = totalSent;
            _lastTrafficSampleTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка проверки трафика: {ex.Message}");
        }
    }

   
    private void CheckInternetSpeed()
    {
       
        if ((DateTime.Now - _lastSpeedTest).TotalMinutes < 5)
        {
           
            _systemCache["DownloadSpeedMbps"] = _lastDownloadSpeedMbps;
            _systemCache["UploadSpeedMbps"] = _lastUploadSpeedMbps;
            return;
        }

        
        if (_isSpeedTestRunning) return;

        _isSpeedTestRunning = true;

        Task.Run(async () =>
        {
            try
            {
               
                double downloadSpeed = await GetInternetSpeedFromApi();

                _lastDownloadSpeedMbps = downloadSpeed;
                _lastUploadSpeedMbps = 0;
                _lastSpeedTest = DateTime.Now;

                _systemCache["DownloadSpeedMbps"] = downloadSpeed;
                _systemCache["UploadSpeedMbps"] = 0;
                _systemCache["DownloadSpeedMBps"] = downloadSpeed / 8;
                _systemCache["UploadSpeedMBps"] = 0;

                Debug.WriteLine($"Internet Speed (API): ↓{downloadSpeed:F1} Мбит/с");

               
                if (downloadSpeed > 0)
                {
                    
                    if (downloadSpeed < 1 && CanSendNotification("SlowInternet", TimeSpan.FromMinutes(30)))
                    {
                        ShowNotification(new SystemNotification
                        {
                            Title = "Медленное интернет-соединение",
                            Message = $"Скорость загрузки: {downloadSpeed:F1} Мбит/с",
                            Icon = "🐢",
                            Type = NotificationType.Warning
                        });
                    }
                   
                    else if (downloadSpeed < 0.5 && CanSendNotification("VerySlowInternet", TimeSpan.FromMinutes(15)))
                    {
                        ShowNotification(new SystemNotification
                        {
                            Title = "Очень медленное интернет-соединение",
                            Message = $"Скорость загрузки: {downloadSpeed:F1} Мбит/с. Проверьте подключение.",
                            Icon = "🐌",
                            Type = NotificationType.Critical
                        });
                    }
                   
                    else if (downloadSpeed > 50 && CanSendNotification("FastInternet", TimeSpan.FromHours(1)))
                    {
                        ShowNotification(new SystemNotification
                        {
                            Title = "Быстрое интернет-соединение",
                            Message = $"Скорость загрузки: {downloadSpeed:F1} Мбит/с",
                            Icon = "🚀",
                            Type = NotificationType.Info
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки скорости: {ex.Message}");
            }
            finally
            {
                _isSpeedTestRunning = false;
            }
        });
    }

    private async Task<double> GetInternetSpeedFromApi()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

          
            var testUrls = new[]
            {
                "https://www.google.com/favicon.ico",
                "https://www.microsoft.com/favicon.ico",
                "https://www.cloudflare.com/favicon.ico"
            };

            double bestSpeed = 0;

            foreach (var url in testUrls)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();
                        stopwatch.Stop();

                        double seconds = stopwatch.Elapsed.TotalSeconds;
                        if (seconds > 0 && content.Length > 0)
                        {
                            // Скорость в Мбит/с
                            double speedMbps = (content.Length * 8) / (1024 * 1024) / seconds;

                            
                            if (speedMbps > 100) speedMbps = 100;

                            if (speedMbps > bestSpeed)
                            {
                                bestSpeed = speedMbps;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка теста {url}: {ex.Message}");
                }
            }

          
            if (bestSpeed < 0.1)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync("google.com", 1000);
                    if (reply.Status == IPStatus.Success && reply.RoundtripTime < 100)
                    {
                        
                        bestSpeed = Math.Max(bestSpeed, 1.0);
                    }
                }
                catch { }
            }

            return bestSpeed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка получения скорости: {ex.Message}");
            return 0;
        }
    }

    private void CheckNetworkAdapters()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .ToList();

            int connectedCount = interfaces.Count;
            _systemCache["ActiveNetworkInterfaces"] = connectedCount;

            foreach (var ni in interfaces)
            {
                string key = $"Adapter_{ni.Name}";

             
                double speedMbps = ni.Speed / 1_000_000.0;
                _systemCache[$"{key}_Speed"] = speedMbps;

               
                try
                {
                    var ipProps = ni.GetIPProperties();
                    var ipv4Addresses = ipProps.UnicastAddresses
                        .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.Address.ToString())
                        .ToList();

                    if (ipv4Addresses.Any())
                    {
                        _systemCache[$"{key}_IP"] = string.Join(", ", ipv4Addresses);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка проверки адаптеров: {ex.Message}");
        }
    }

  
    private void CheckOpenPorts()
    {
        try
        {
            
            if (_isWindows)
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-an",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };

                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(1000);

             
                var matches = Regex.Matches(output, @":(\d+)\s+.*LISTENING");
                var openPorts = matches
                    .Cast<Match>()
                    .Select(m => int.Parse(m.Groups[1].Value))
                    .Distinct()
                    .ToList();

                int portCount = openPorts.Count;
                _systemCache["OpenPorts"] = portCount;
                _systemCache["OpenPortsList"] = openPorts.Take(20).ToList();

                Debug.WriteLine($"Open ports: {portCount}");

              
                if (portCount > 100 && CanSendNotification("OpenPorts", TimeSpan.FromHours(1)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Много открытых портов",
                        Message = $"Обнаружено {portCount} открытых портов. Возможно, запущено много сервисов.",
                        Icon = "🚪",
                        Type = NotificationType.Info
                    });
                }

                
                var dangerousPorts = openPorts.Intersect(new[] { 21, 22, 23, 25, 80, 443, 3306, 3389, 5900, 6379, 27017 });
                if (dangerousPorts.Any() && CanSendNotification("DangerousPorts", TimeSpan.FromHours(1)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Открыты стандартные порты",
                        Message = $"Открыты порты: {string.Join(", ", dangerousPorts)}. Проверьте безопасность системы.",
                        Icon = "🔓",
                        Type = NotificationType.Warning
                    });
                }
            }
            else if (_isLinux || _isMacOs)
            {
               
                string command = _isLinux ? "ss" : "netstat";
                string args = _isLinux ? "-tuln" : "-an | grep LISTEN";

                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };

                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(1000);

               
                var portMatches = Regex.Matches(output, @":(\d+)\s+");
                var openPorts = portMatches
                    .Cast<Match>()
                    .Select(m => int.Parse(m.Groups[1].Value))
                    .Distinct()
                    .ToList();

                int portCount = openPorts.Count;
                _systemCache["OpenPorts"] = portCount;
                _systemCache["OpenPortsList"] = openPorts.Take(20).ToList();

                Debug.WriteLine($"Open ports: {portCount}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка проверки открытых портов: {ex.Message}");
            _systemCache["OpenPorts"] = -1;
        }
    }

   
    public bool IsInternetAvailable()
    {
        return CheckInternetAccess();
    }

    /
    public (double DownloadMbps, double UploadMbps) GetRealInternetSpeed()
    {
        return (_lastDownloadSpeedMbps, _lastUploadSpeedMbps);
    }

    
    public (double DownloadMBps, double UploadMBps) GetCurrentNetworkTraffic()
    {
        if (_systemCache.TryGetValue("CurrentDownloadMBps", out object? dl) &&
            _systemCache.TryGetValue("CurrentUploadMBps", out object? ul))
        {
            return ((double)dl, (double)ul);
        }
        return (0, 0);
    }

   
    public (double Download, double Upload) GetNetworkSpeed()
    {
        if (_systemCache.TryGetValue("DownloadSpeedMBps", out object? dl) &&
            _systemCache.TryGetValue("UploadSpeedMBps", out object? ul))
        {
            return ((double)dl, (double)ul);
        }
        return (0, 0);
    }

    #endregion

    #region Battery Monitoring

    private void CheckBattery()
    {
        try
        {
            int batteryPercent = -1;
            bool isDischarging = false;
            bool isCharging = false;

            if (_isLinux)
            {
                (batteryPercent, isDischarging, isCharging) = GetLinuxBatteryStatus();
            }
            else if (_isWindows)
            {
                (batteryPercent, isDischarging, isCharging) = GetWindowsBatteryStatus();
            }
            else if (_isMacOs)
            {
                (batteryPercent, isDischarging, isCharging) = GetMacBatteryStatus();
            }

            if (batteryPercent >= 0)
            {
                _systemCache["BatteryPercent"] = batteryPercent;
                _systemCache["BatteryDischarging"] = isDischarging;
                _systemCache["BatteryCharging"] = isCharging;

                if (batteryPercent < 15 && isDischarging && CanSendNotification("Battery_Low", TimeSpan.FromMinutes(5)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = LocalizationService.Instance["LowBatteryTitle"] ?? "Низкий заряд аккумулятора",
                        Message = string.Format(LocalizationService.Instance["LowBatteryMessage"] ?? "Заряд батареи: {0}%", batteryPercent),
                        Icon = "🪫",
                        Type = NotificationType.Warning
                    });
                }
                else if (batteryPercent < 5 && isDischarging && CanSendNotification("Battery_Critical", TimeSpan.FromMinutes(1)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Критический заряд батареи",
                        Message = $"Заряд батареи: {batteryPercent}% - подключите зарядное устройство!",
                        Icon = "🔋",
                        Type = NotificationType.Critical
                    });
                }
                else if (batteryPercent >= 95 && isCharging && CanSendNotification("Battery_Full", TimeSpan.FromHours(1)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Батарея заряжена",
                        Message = $"Заряд батареи: {batteryPercent}%",
                        Icon = "✅",
                        Type = NotificationType.Info
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга батареи: {ex.Message}");
        }
    }

    private (int Percent, bool Discharging, bool Charging) GetLinuxBatteryStatus()
    {
        try
        {
            if (!Directory.Exists("/sys/class/power_supply")) return (-1, false, false);

            var batDirs = Directory.GetDirectories("/sys/class/power_supply", "BAT*");
            foreach (var batDir in batDirs)
            {
                string capacityPath = Path.Combine(batDir, "capacity");
                string statusPath = Path.Combine(batDir, "status");

                if (File.Exists(capacityPath) && int.TryParse(File.ReadAllText(capacityPath).Trim(), out int capacity))
                {
                    string status = File.Exists(statusPath) ? File.ReadAllText(statusPath).Trim() : "";
                    bool discharging = status.Equals("Discharging", StringComparison.OrdinalIgnoreCase);
                    bool charging = status.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                                   status.Equals("Full", StringComparison.OrdinalIgnoreCase);
                    return (capacity, discharging, charging);
                }
            }
        }
        catch { }

        return (-1, false, false);
    }

    private (int Percent, bool Discharging, bool Charging) GetWindowsBatteryStatus()
    {
        try
        {
            if (GetSystemPowerStatus(out var status))
            {
                if (status.BatteryLifePercent != 255)
                {
                    int percent = status.BatteryLifePercent;
                    bool discharging = status.ACLineStatus == 0;
                    bool charging = status.ACLineStatus == 1;
                    return (percent, discharging, charging);
                }
            }
        }
        catch { }

        return (-1, false, false);
    }

    private (int Percent, bool Discharging, bool Charging) GetMacBatteryStatus()
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pmset",
                    Arguments = "-g batt",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(500);

           
            var percentMatch = Regex.Match(output, @"(\d+)%");
            var statusMatch = Regex.Match(output, @"'(.*?)'");

            if (percentMatch.Success)
            {
                int percent = int.Parse(percentMatch.Groups[1].Value);
                string status = statusMatch.Success ? statusMatch.Groups[1].Value : "";
                bool discharging = status.Contains("discharging");
                bool charging = status.Contains("charging") || status.Contains("AC");
                return (percent, discharging, charging);
            }
        }
        catch { }

        return (-1, false, false);
    }

    #endregion

    #region Process Monitoring

    private void CheckProcessProblems()
    {
        try
        {
            var processes = Process.GetProcesses();
            int notResponding = 0;

            foreach (var process in processes.Take(50))
            {
                try
                {
                   
                    if (_isWindows && !process.Responding && process.Id != Process.GetCurrentProcess().Id)
                    {
                        notResponding++;
                        string key = $"Process_{process.Id}";
                        if (CanSendNotification(key, TimeSpan.FromMinutes(5)))
                        {
                            ShowNotification(new SystemNotification
                            {
                                Title = LocalizationService.Instance["ProcessNotRespondingTitle"] ?? "Процесс не отвечает",
                                Message = string.Format(LocalizationService.Instance["ProcessNotRespondingMessage"] ?? "Приложение '{0}' зависло.", process.ProcessName),
                                Icon = "💀",
                                Type = NotificationType.Warning
                            });
                        }
                        break;
                    }
                }
                catch { }
            }

            _systemCache["NotRespondingProcesses"] = notResponding;
            _systemCache["TotalProcesses"] = processes.Length;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка проверки процессов: {ex.Message}");
        }
    }

    private void CheckRunningServices()
    {
        try
        {
            if (!_isLinux) return;

            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = "list-units --type=service --state=failed",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1000);

            var failedServices = Regex.Matches(output, @"^\s*●\s+(\S+\.service)")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            if (failedServices.Count > 0 && CanSendNotification("FailedServices", TimeSpan.FromHours(1)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = "Неработающие системные службы",
                    Message = $"Обнаружено {failedServices.Count} неработающих служб: {string.Join(", ", failedServices.Take(3))}",
                    Icon = "⚠️",
                    Type = NotificationType.Warning
                });
            }

            _systemCache["FailedServices"] = failedServices.Count;
        }
        catch { }
    }

    #endregion

    #region System Load & Uptime

    private void CheckSystemUptime()
    {
        try
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            if (uptime.TotalDays > 7 && CanSendNotification("Uptime_Warning", TimeSpan.FromHours(24)))
            {
                ShowNotification(new SystemNotification
                {
                    Title = "Длительное время работы системы",
                    Message = $"Система работает уже {uptime.Days} дней без перезагрузки. Рекомендуется перезагрузить ПК.",
                    Icon = "⏰",
                    Type = NotificationType.Info
                });
            }

            _systemCache["SystemUptime"] = uptime;
        }
        catch { }
    }

    private void CheckSystemLoad()
    {
        try
        {
            if (!_isLinux && !_isMacOs) return;

            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _isLinux ? "uptime" : "sysctl",
                    Arguments = _isLinux ? "" : "-n vm.loadavg",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(500);

            if (_isLinux)
            {
                var match = Regex.Match(output, @"load average:\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)");
                if (match.Success)
                {
                    float load1 = float.Parse(match.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    float load5 = float.Parse(match.Groups[2].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    float load15 = float.Parse(match.Groups[3].Value,
                        System.Globalization.CultureInfo.InvariantCulture);

                    int cores = Environment.ProcessorCount;

                    if (load1 > cores * 1.5 && CanSendNotification("SystemLoad", TimeSpan.FromMinutes(5)))
                    {
                        ShowNotification(new SystemNotification
                        {
                            Title = "Высокая загрузка системы",
                            Message = $"Средняя загрузка за 1 минуту: {load1:F1} (ядер: {cores})",
                            Icon = "📊",
                            Type = NotificationType.Warning
                        });
                    }

                    _systemCache["LoadAvg1"] = load1;
                    _systemCache["LoadAvg5"] = load5;
                    _systemCache["LoadAvg15"] = load15;
                }
            }
            else if (_isMacOs)
            {
                var match = Regex.Match(output, @"([\d.]+) ([\d.]+) ([\d.]+)");
                if (match.Success)
                {
                    float load1 = float.Parse(match.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    float load5 = float.Parse(match.Groups[2].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    float load15 = float.Parse(match.Groups[3].Value,
                        System.Globalization.CultureInfo.InvariantCulture);

                    int cores = Environment.ProcessorCount;

                    if (load1 > cores * 1.5 && CanSendNotification("SystemLoad", TimeSpan.FromMinutes(5)))
                    {
                        ShowNotification(new SystemNotification
                        {
                            Title = "Высокая загрузка системы",
                            Message = $"Средняя загрузка за 1 минуту: {load1:F1} (ядер: {cores})",
                            Icon = "📊",
                            Type = NotificationType.Warning
                        });
                    }

                    _systemCache["LoadAvg1"] = load1;
                    _systemCache["LoadAvg5"] = load5;
                    _systemCache["LoadAvg15"] = load15;
                }
            }
        }
        catch { }
    }

    #endregion

    #region Temperature Monitoring

    private void CheckTemperature()
    {
        try
        {
            float temp = -1;

            if (_isLinux)
            {
                temp = GetLinuxTemperature();
            }
            else if (_isMacOs)
            {
                temp = GetMacTemperature();
            }
            else if (_isWindows)
            {
                temp = GetWindowsTemperature();
            }

            if (temp > 0)
            {
                _systemCache["CurrentTemperature"] = temp;

                if (temp > 85 && CanSendNotification("Temp_Critical", TimeSpan.FromMinutes(5)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Критическая температура системы",
                        Message = $"Температура достигла {temp:F1}°C!",
                        Icon = "🌡️",
                        Type = NotificationType.Critical
                    });
                }
                else if (temp > 75 && CanSendNotification("Temp_Warning", TimeSpan.FromMinutes(10)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Высокая температура системы",
                        Message = $"Температура: {temp:F1}°C",
                        Icon = "🔥",
                        Type = NotificationType.Warning
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга температуры: {ex.Message}");
        }
    }

    private float GetLinuxTemperature()
    {
        try
        {
            if (!Directory.Exists("/sys/class/thermal")) return -1;

            var zones = Directory.GetDirectories("/sys/class/thermal", "thermal_zone*");
            foreach (var zone in zones)
            {
                string tempPath = Path.Combine(zone, "temp");
                string typePath = Path.Combine(zone, "type");

                if (File.Exists(tempPath) && long.TryParse(File.ReadAllText(tempPath).Trim(), out long rawTemp))
                {
                   
                    string type = File.Exists(typePath) ? File.ReadAllText(typePath).Trim() : "";
                    if (type.Contains("cpu") || type.Contains("x86") || type.Contains("pkg") || string.IsNullOrEmpty(type))
                    {
                        return rawTemp / 1000f;
                    }
                }
            }
        }
        catch { }

        return -1;
    }

    private float GetMacTemperature()
    {
        try
        {
           
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = "powermetrics --samplers smc -n1",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1000);

            var match = Regex.Match(output, @"CPU die temperature:\s*([\d.]+) C");
            if (match.Success)
            {
                return float.Parse(match.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch { }

        return -1;
    }

    private float GetWindowsTemperature()
    {
        try
        {
           
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "/namespace:\\\\root\\wmi PATH MSAcpi_ThermalZoneTemperature get CurrentTemperature",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1000);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (int.TryParse(line.Trim(), out int temp) && temp > 0 && temp < 10000)
                {
                    return (temp - 2732) / 10f;
                }
            }
        }
        catch { }

        return -1;
    }

    #endregion

    #region GPU Monitoring

    private void CheckGPUUsage()
    {
        try
        {
            // Проверяем не чаще раза в минуту
            if ((DateTime.Now - _lastGpuCheck).TotalSeconds < 60) return;
            _lastGpuCheck = DateTime.Now;

            float gpuUsage = -1;

            if (_isNvidiaSmiAvailable != false)
            {
                gpuUsage = GetNvidiaGpuUsage();
                if (gpuUsage >= 0)
                {
                    _isNvidiaSmiAvailable = true;
                }
            }

            if (gpuUsage < 0 && _isRadeonAvailable != false)
            {
                gpuUsage = GetRadeonGpuUsage();
                if (gpuUsage >= 0)
                {
                    _isRadeonAvailable = true;
                }
            }

            if (gpuUsage > 0)
            {
                _systemCache["GPUUsage"] = gpuUsage;

                if (gpuUsage > 95 && CanSendNotification("GPU_Critical", TimeSpan.FromMinutes(3)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Критическая загрузка ГП",
                        Message = $"Загрузка видеокарты: {gpuUsage:F1}%",
                        Icon = "🎮",
                        Type = NotificationType.Critical
                    });
                }
                else if (gpuUsage > 85 && CanSendNotification("GPU_Warning", TimeSpan.FromMinutes(5)))
                {
                    ShowNotification(new SystemNotification
                    {
                        Title = "Высокая загрузка ГП",
                        Message = $"Загрузка видеокарты: {gpuUsage:F1}%",
                        Icon = "⚡",
                        Type = NotificationType.Warning
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка мониторинга GPU: {ex.Message}");
        }
    }

    private float GetNvidiaGpuUsage()
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=utilization.gpu --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(500);

            if (float.TryParse(output, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float usage))
            {
                return usage;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _isNvidiaSmiAvailable = false;
        }
        catch { }

        return -1;
    }

    private float GetRadeonGpuUsage()
    {
        try
        {
            if (_isLinux && File.Exists("/sys/class/drm/card0/device/gpu_busy_percent"))
            {
                string content = File.ReadAllText("/sys/class/drm/card0/device/gpu_busy_percent").Trim();
                if (float.TryParse(content, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float usage))
                {
                    return usage;
                }
            }
        }
        catch
        {
            _isRadeonAvailable = false;
        }

        return -1;
    }

    #endregion

    #region Helpers

    private bool CanSendNotification(string key, TimeSpan cooldown)
    {
        DateTime now = DateTime.Now;
        if (_lastNotificationTimes.TryGetValue(key, out DateTime lastTime))
        {
            if (now - lastTime < cooldown) return false;
        }

        _lastNotificationTimes[key] = now;
        return true;
    }

    private void ShowNotification(SystemNotification notification)
    {
        _sentNotifications.Add(notification);
        OnNotification?.Invoke(notification);
    }

    public T? GetCachedValue<T>(string key) where T : struct
    {
        if (_systemCache.TryGetValue(key, out object? value))
        {
            if (value is T typedValue)
                return typedValue;
        }
        return null;
    }

    #endregion

    #region P/Invoke для Windows

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    #endregion
}