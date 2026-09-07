using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Black_Hole_Cross.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Input.Platform;



namespace Black_Hole_Cross.ControlDiagnostic
{
    #region Win32 API Interop (Only for Windows)
    internal static class Win32RamNative
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
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
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    }
    #endregion

    public static class CrossPlatformMemory
    {
        public struct MemoryInfo
        {
            public ulong TotalBytes;
            public ulong FreeBytes;
            public ulong UsedBytes;
            public ulong SwapTotalBytes;
            public ulong SwapFreeBytes;
            public ulong CacheBytes;
        }

        public static MemoryInfo GetMemoryInfo()
        {
            var info = new MemoryInfo();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var statEx = new Win32RamNative.MEMORYSTATUSEX();
                if (Win32RamNative.GlobalMemoryStatusEx(statEx))
                {
                    info.TotalBytes = statEx.ullTotalPhys;
                    info.FreeBytes = statEx.ullAvailPhys;
                    info.UsedBytes = statEx.ullTotalPhys - statEx.ullAvailPhys;
                    info.SwapTotalBytes = statEx.ullTotalPageFile;
                    info.SwapFreeBytes = statEx.ullAvailPageFile;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                GetLinuxMemoryInfo(ref info);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                GetMacMemoryInfo(ref info);
            }

            return info;
        }

        private static void GetLinuxMemoryInfo(ref MemoryInfo info)
        {
            try
            {
                if (!File.Exists("/proc/meminfo")) return;

                var lines = File.ReadAllLines("/proc/meminfo");
                var memData = new Dictionary<string, ulong>();

                foreach (var line in lines)
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var valueStr = parts[1].Trim().Split(' ')[0];
                        if (ulong.TryParse(valueStr, out ulong kb))
                        {
                            memData[key] = kb * 1024; // переводим КБ в Байты
                        }
                    }
                }

                memData.TryGetValue("MemTotal", out info.TotalBytes);
                memData.TryGetValue("MemAvailable", out info.FreeBytes);
                if (info.FreeBytes == 0) memData.TryGetValue("MemFree", out info.FreeBytes);

                info.UsedBytes = info.TotalBytes > info.FreeBytes ? info.TotalBytes - info.FreeBytes : 0;

                memData.TryGetValue("SwapTotal", out info.SwapTotalBytes);
                memData.TryGetValue("SwapFree", out info.SwapFreeBytes);

                memData.TryGetValue("Cached", out ulong cached);
                memData.TryGetValue("Buffers", out ulong buffers);
                info.CacheBytes = cached + buffers;
            }
            catch { }
        }

        private static void GetMacMemoryInfo(ref MemoryInfo info)
        {
            try
            {
                string totalStr = ExecuteCommand("sysctl", "-n hw.memsize");
                if (ulong.TryParse(totalStr, out ulong totalBytes))
                {
                    info.TotalBytes = totalBytes;
                }

                string vmStat = ExecuteCommand("vm_stat", "");
                if (!string.IsNullOrEmpty(vmStat))
                {
                    ulong pageSize = 4096;
                    string pageSizeStr = ExecuteCommand("sysctl", "-n hw.pagesize");
                    ulong.TryParse(pageSizeStr, out pageSize);

                    ulong freePages = 0, activePages = 0, inactivePages = 0, wiredPages = 0, purgeablePages = 0, specPages = 0;

                    foreach (var line in vmStat.Split('\n'))
                    {
                        var parts = line.Split(':');
                        if (parts.Length < 2) continue;
                        string key = parts[0].Trim();
                        string val = parts[1].Replace(".", "").Trim();

                        if (ulong.TryParse(val, out ulong count))
                        {
                            if (key.Contains("Pages free")) freePages = count;
                            else if (key.Contains("Pages active")) activePages = count;
                            else if (key.Contains("Pages inactive")) inactivePages = count;
                            else if (key.Contains("Pages wired down")) wiredPages = count;
                            else if (key.Contains("Pages purgeable")) purgeablePages = count;
                            else if (key.Contains("File-backed pages") || key.Contains("Pages speculative")) specPages = count;
                        }
                    }

                    info.FreeBytes = (freePages + inactivePages) * pageSize;
                    info.UsedBytes = (activePages + wiredPages) * pageSize;
                    info.CacheBytes = (purgeablePages + specPages) * pageSize;
                }
            }
            catch { }
        }

        private static string ExecuteCommand(string cmd, string args)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                string res = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return res;
            }
            catch { return ""; }
        }
    }

    public partial class RamControl : UserControl
    {
        public ObservableCollection<DataCopyInfo> HistoryList { get; set; } = new ObservableCollection<DataCopyInfo>();
        private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "historyCopy.json");
        private string _lastText = "";

        // Performance counters (Windows fallback)
        private static PerformanceCounter _pfCounter;
        private static PerformanceCounter _psCounter;
        private static PerformanceCounter _cacheCounter;

        public RamControl()
        {
            InitializeComponent();

            UpdateChart();
            DiagnosRAM.Text = LocalizationService.Instance["RamDiagnostics"];
            ModelAndManuf.Content = LocalizationService.Instance["ModelAndManufacturer"];
            UsageRAM.Content = LocalizationService.Instance["RamUsage"];
            LoadHistory();

            this.DataContext = this;
            this.Unloaded += OnControlUnloaded;

            this.AttachedToVisualTree += async (s, e) =>
            {
                SystemMonitorService.Instance.OnTick += OnSystemTick;
                OnSystemTick();

                var modules = await Task.Run(() => GetPhysicalMemory());

                if (modules.Count == 0)
                {
                    RamInfo.Text = "Не удалось получить детальные данные о модулях памяти.\n(Требуются права Администратора/Root или ограничение гипервизора)";
                    return;
                }

                var sb = new StringBuilder();
                foreach (var module in modules)
                {
                    sb.AppendLine(module.ToString());
                    sb.AppendLine("─────────────────────");
                }
                RamInfo.Text = sb.ToString();
            };
        }

        private void OnSystemTick()
        {
            UpdateChart();
            _ = CheckClipBoardAsync();

            var memInfo = CrossPlatformMemory.GetMemoryInfo();
            var (pf, ps) = GetPageFaultsMetrics();

            double cacheGb = memInfo.CacheBytes / (1024.0 * 1024.0 * 1024.0);
            double swapTotalGb = memInfo.SwapTotalBytes / (1024.0 * 1024.0 * 1024.0);
            double swapFreeGb = memInfo.SwapFreeBytes / (1024.0 * 1024.0 * 1024.0);
            double swapUsedGb = swapTotalGb - swapFreeGb;

            var sb = new StringBuilder();
            sb.AppendLine($" {LocalizationService.Instance["LiveStatus"]}");
            sb.AppendLine("─────────────────────");
            sb.AppendLine($" {LocalizationService.Instance["PageFaults"]}: {pf:N0}");
            sb.AppendLine($" {LocalizationService.Instance["PagesPerSec"]}: {ps:N0}");
            sb.AppendLine($" {LocalizationService.Instance["Cache"]}: {cacheGb:F2} GB");

            if (swapTotalGb > 0)
            {
                sb.AppendLine($" Файл подкачки (Swap): {swapUsedGb:F2} / {swapTotalGb:F2} GB");
            }

            RamLiveData.Text = sb.ToString();
        }

        private (float pf, float ps) GetPageFaultsMetrics()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _pfCounter ??= new PerformanceCounter("Memory", "Page Faults/sec");
                    _psCounter ??= new PerformanceCounter("Memory", "Pages/sec");
                    return (_pfCounter.NextValue(), _psCounter.NextValue());
                }
                catch { }
            }
            return (0, 0);
        }

        private void UpdateChart()
        {
            var mem = CrossPlatformMemory.GetMemoryInfo();
            double usedGb = mem.UsedBytes / (1024.0 * 1024.0 * 1024.0);
            double freeGb = mem.FreeBytes / (1024.0 * 1024.0 * 1024.0);

            RamPieChart.Series = new ObservableCollection<ISeries>
            {
                new PieSeries<double>
                {
                    Name = $"{LocalizationService.Instance["Used"]}",
                    Values = new double[] { usedGb },
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:F2} GB"
                },
                new PieSeries<double>
                {
                    Name = $"{LocalizationService.Instance["Free"]}",
                    Values = new double[] { freeGb },
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:F2} GB"
                }
            };
        }

        #region Cross-Platform RAM Hardware Details
        public class RamModuleInfo
        {
            public string Slot { get; set; } = "N/A";
            public string Manufacturer { get; set; } = "Unknown";
            public string PartNumber { get; set; } = "N/A";
            public string SerialNumber { get; set; } = "N/A";
            public ulong CapacityBytes { get; set; }
            public int SpeedMHz { get; set; }
            public string MemoryType { get; set; } = "Unknown";
            public string FormFactor { get; set; } = "Unknown";
            public string ConfiguredVoltage { get; set; } = "N/A";

            public string CapacityGB => CapacityBytes > 0
                ? (CapacityBytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.##") + " GB"
                : "Unknown";

            public override string ToString()
            {
                return $"{LocalizationService.Instance["Slot"]}: {Slot}\n" +
                       $"{LocalizationService.Instance["Manufacturer"]}: {Manufacturer}\n" +
                       $"{LocalizationService.Instance["PartNumber"]}: {PartNumber}\n" +
                       $"{LocalizationService.Instance["SerialNumber"]}: {SerialNumber}\n" +
                       $"{LocalizationService.Instance["Capacity"]}: {CapacityGB}\n" +
                       $"{LocalizationService.Instance["Speed"]}: {(SpeedMHz > 0 ? SpeedMHz + " MHz" : "Unknown")}\n" +
                       $"{LocalizationService.Instance["MemoryType"]}: {MemoryType}\n" +
                       $"{LocalizationService.Instance["FormFactor"]}: {FormFactor}\n" +
                       $"Напряжение питания: {ConfiguredVoltage}";
            }
        }

        public static List<RamModuleInfo> GetPhysicalMemory()
        {
            var list = new List<RamModuleInfo>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GetWindowsRamModules(list);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                GetLinuxRamModules(list);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                GetMacRamModules(list);
            }

            return list;
        }

        private static void GetWindowsRamModules(List<RamModuleInfo> list)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PhysicalMemory");
                var results = searcher.Get().Cast<ManagementObject>().ToList();
                string channelMode = results.Count > 1 ? "Multi-Channel" : "Single-Channel";

                foreach (ManagementObject obj in results)
                {
                    var rawType = obj["SMBIOSMemoryType"] ?? obj["MemoryType"];
                    ushort typeCode = rawType != null ? Convert.ToUInt16(rawType) : (ushort)0;
                    var speed = obj["ConfiguredClockSpeed"] ?? obj["Speed"];
                    var voltage = obj["ConfiguredVoltage"];

                    var info = new RamModuleInfo
                    {
                        Slot = obj["DeviceLocator"]?.ToString() ?? "DIMM",
                        Manufacturer = obj["Manufacturer"]?.ToString().Trim() ?? "Unknown",
                        PartNumber = obj["PartNumber"]?.ToString().Trim() ?? "N/A",
                        SerialNumber = obj["SerialNumber"]?.ToString().Trim() ?? "N/A",
                        SpeedMHz = speed != null ? Convert.ToInt32(speed) : 0,
                        MemoryType = $"{GetMemoryTypeByCode(typeCode)} ({channelMode})",
                        FormFactor = obj["FormFactor"] != null ? GetFormFactorByCode(Convert.ToUInt16(obj["FormFactor"])) : "Unknown",
                        ConfiguredVoltage = voltage != null ? $"{Convert.ToDouble(voltage) / 1000.0:F2} V" : "N/A"
                    };

                    if (obj["Capacity"] != null && ulong.TryParse(obj["Capacity"].ToString(), out ulong cap))
                    {
                        info.CapacityBytes = cap;
                    }

                    list.Add(info);
                }
            }
            catch { }
        }

        private static void GetLinuxRamModules(List<RamModuleInfo> list)
        {
            try
            {
             
                string output = ExecuteSystemCommand("sudo", "dmidecode -t memory");
                if (string.IsNullOrEmpty(output) || output.Contains("Permission denied"))
                {
                    output = ExecuteSystemCommand("dmidecode", "-t memory");
                }

                if (!string.IsNullOrEmpty(output))
                {
                    var devices = output.Split(new[] { "Memory Device" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var dev in devices.Skip(1))
                    {
                        if (dev.Contains("No Module Installed")) continue;

                        var info = new RamModuleInfo();
                        foreach (var line in dev.Split('\n'))
                        {
                            var parts = line.Split(':');
                            if (parts.Length < 2) continue;
                            string k = parts[0].Trim();
                            string v = parts[1].Trim();

                            if (k == "Locator") info.Slot = v;
                            else if (k == "Manufacturer") info.Manufacturer = v;
                            else if (k == "Part Number") info.PartNumber = v;
                            else if (k == "Serial Number") info.SerialNumber = v;
                            else if (k == "Type") info.MemoryType = v;
                            else if (k == "Form Factor") info.FormFactor = v;
                            else if (k == "Configured Voltage") info.ConfiguredVoltage = v;
                            else if (k == "Speed" && v.Contains("MHz"))
                            {
                                int.TryParse(v.Split(' ')[0], out int speed);
                                info.SpeedMHz = speed;
                            }
                            else if (k == "Size" && (v.Contains("MB") || v.Contains("GB")))
                            {
                                var sizeParts = v.Split(' ');
                                if (double.TryParse(sizeParts[0], out double sz))
                                {
                                    info.CapacityBytes = v.Contains("GB")
                                        ? (ulong)(sz * 1024 * 1024 * 1024)
                                        : (ulong)(sz * 1024 * 1024);
                                }
                            }
                        }
                        if (info.CapacityBytes > 0) list.Add(info);
                    }
                }
            }
            catch { }
        }

        private static void GetMacRamModules(List<RamModuleInfo> list)
        {
            try
            {
                string output = ExecuteSystemCommand("system_profiler", "SPMemoryDataType");
                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n');
                    RamModuleInfo current = null;

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("BANK") || trimmed.StartsWith("DIMM"))
                        {
                            current = new RamModuleInfo { Slot = trimmed.Replace(":", "") };
                            list.Add(current);
                        }
                        else if (current != null && trimmed.Contains(":"))
                        {
                            var parts = trimmed.Split(':');
                            string k = parts[0].Trim();
                            string v = parts[1].Trim();

                            if (k == "Size" && ulong.TryParse(v.Split(' ')[0], out ulong gb)) current.CapacityBytes = gb * 1024 * 1024 * 1024;
                            else if (k == "Type") current.MemoryType = v;
                            else if (k == "Speed" && int.TryParse(v.Split(' ')[0], out int sp)) current.SpeedMHz = sp;
                            else if (k == "Manufacturer") current.Manufacturer = v;
                            else if (k == "Part Number") current.PartNumber = v;
                            else if (k == "Serial Number") current.SerialNumber = v;
                        }
                    }
                }
            }
            catch { }
        }

        private static string GetMemoryTypeByCode(ushort code)
        {
            return code switch
            {
                20 => "DDR",
                21 => "DDR2",
                22 => "DDR2 FB-DIMM",
                24 => "DDR3",
                26 => "DDR4",
                30 => "LPDDR4",
                34 => "DDR5",
                35 => "LPDDR5",
                _ => "DDR/Unknown"
            };
        }

        private static string GetFormFactorByCode(ushort code)
        {
            return code switch
            {
                8 => "DIMM",
                12 => "SO-DIMM",
                9 => "SMD",
                10 => "Chip",
                16 => "RIMM",
                17 => "SODIMM",
                _ => "Unknown"
            };
        }
        #endregion

        #region Clipboard & History Management
        public class DataCopyInfo
        {
            public string Type { get; set; }
            public string Content { get; set; }
            public string FileName { get; set; }
            public string SourceApp { get; set; }
            public DateTime Time { get; set; } = DateTime.Now;

            public string TimeLabel => Time.ToString("HH:mm / dd.MM.yyyy");
        }

        private void SaveHistory()
        {
            try
            {
                var listToSave = HistoryList.ToList();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(listToSave, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка записи истории: " + ex.Message);
            }
        }

        private void LoadHistory()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<List<DataCopyInfo>>(json);
                if (loaded != null)
                {
                    HistoryList.Clear();
                    foreach (var item in loaded) HistoryList.Add(item);

                    var lastTextEntry = loaded.FirstOrDefault(x => x.Type.Contains("Text"));
                    if (lastTextEntry != null) _lastText = lastTextEntry.Content;
                }
            }
            catch (Exception ex) { Debug.WriteLine("Ошибка загрузки истории: " + ex.Message); }
        }

        #region Clipboard & History Management

        public async Task CheckClipBoardAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard == null) return;

                DataCopyInfo entry = null;
                var clipboard = topLevel.Clipboard;

               
                try
                {
                    var formats = await clipboard.GetFormatsAsync();
                    if (formats != null && (formats.Contains("PNG") || formats.Contains("Bitmap") || formats.Contains("DeviceIndependentBitmap")))
                    {
                      
                        string currentImageId = $"img_{DateTime.Now:yyyyMMdd_HHmm}";
                        if (_lastText != currentImageId)
                        {
                            _lastText = currentImageId;
                            entry = new DataCopyInfo
                            {
                                Type = "Image 🖼️",
                                Content = "Буфер обмена (Изображение)",
                                FileName = "Скопированная картинка",
                                SourceApp = GetActiveAppName()
                            };
                        }
                    }
                }
                catch { }

             
                if (entry == null)
                {
                    string currentText = await clipboard.GetTextAsync();

                    if (!string.IsNullOrWhiteSpace(currentText) && currentText != _lastText)
                    {
                        _lastText = currentText;
                        string trimmed = currentText.Trim();

                        if ((trimmed.Length < 300) && (File.Exists(trimmed) || Directory.Exists(trimmed)))
                        {
                            string path = trimmed;
                            string ext = Path.GetExtension(path).ToLower();
                            string typeName = "File 📁";

                            if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" }.Contains(ext)) typeName = "Image 🖼️";
                            else if (new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm" }.Contains(ext)) typeName = "Video 🎬";
                            else if (new[] { ".mp3", ".wav", ".flac", ".ogg", ".aac" }.Contains(ext)) typeName = "Audio 🎵";
                            else if (new[] { ".blend", ".obj", ".fbx", ".stl", ".dae", ".gltf" }.Contains(ext)) typeName = "3D Model 🧊";
                            else if (new[] { ".txt", ".json", ".xml", ".cs", ".py", ".md" }.Contains(ext)) typeName = "Code/Text 📄";

                            entry = new DataCopyInfo
                            {
                                Type = typeName,
                                Content = path,
                                FileName = Path.GetFileName(path),
                                SourceApp = GetActiveAppName()
                            };
                        }
                        else
                        {
                            entry = new DataCopyInfo
                            {
                                Type = "Text 📋",
                                Content = currentText,
                                FileName = currentText.Length > 35 ? currentText.Substring(0, 32) + "..." : currentText,
                                SourceApp = GetActiveAppName()
                            };
                        }
                    }
                }

                // Запись в историю
                if (entry != null && (HistoryList.Count == 0 || HistoryList[0].Content != entry.Content))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        HistoryList.Insert(0, entry);
                        if (HistoryList.Count > 20) HistoryList.RemoveAt(20);
                        SaveHistory();
                    });
                }
            }
            catch { }
        }

        private void ItemBorder_MouseEnter(object? sender, PointerEventArgs e)
        {
            if ((sender as Border)?.DataContext is not DataCopyInfo item) return;

           
            PopupText.IsVisible = false;
            PopupImage.IsVisible = false;
            PopupFilePath.IsVisible = false;
            PopupMediaPanel.IsVisible = false;
            Popup3DPanel.IsVisible = false;

            PopupTitle.Text = $"{item.Type.ToUpper()} | {item.TimeLabel}";
            string type = item.Type.ToLower();

            if (type.Contains("image"))
            {
                PopupImage.IsVisible = true;
                if (File.Exists(item.Content))
                {
                    try
                    {
                        PopupImage.Source = new Bitmap(item.Content);
                    }
                    catch { }
                }
            }
            else if (type.Contains("video") || type.Contains("audio"))
            {
                PopupMediaPanel.IsVisible = true;
                PopupMediaInfo.Text = $"Файл: {item.FileName}\nПуть: {item.Content}";
            }
            else if (type.Contains("3d model"))
            {
                Popup3DPanel.IsVisible = true;
                Popup3DInfo.Text = $"3D Объект Blender/CAD\nИмя: {item.FileName}";
            }
            else if (type.Contains("text"))
            {
                PopupText.IsVisible = true;
                PopupText.Text = item.Content;
            }
            else
            {
                PopupFilePath.IsVisible = true;
                PopupFilePath.Text = "Путь к файлу:\n" + item.Content;
            }

            if (!string.IsNullOrEmpty(item.SourceApp))
                PopupTitle.Text += $" (из {item.SourceApp})";

           
            PreviewPopup.PlacementTarget = sender as Control;
            PreviewPopup.IsOpen = true;
        }

        private void ItemBorder_MouseLeave(object? sender, PointerEventArgs e)
        {
            PreviewPopup.IsOpen = false;
        }

        private async void BtnRestore_Click(object? sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DataCopyInfo item)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(item.Content);
                }
            }
        }

        private void BtnDelete_Click(object? sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DataCopyInfo item)
            {
                HistoryList.Remove(item);
                SaveHistory();
            }
        }

        #endregion

        private string GetActiveAppName()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    IntPtr hwnd = Win32RamNative.GetForegroundWindow();
                    Win32RamNative.GetWindowThreadProcessId(hwnd, out int pid);
                    var process = Process.GetProcessById(pid);

                    string appName = process.ProcessName;
                    return (appName.Equals("BlackHole", StringComparison.OrdinalIgnoreCase) ||
                            appName.Equals("devenv", StringComparison.OrdinalIgnoreCase))
                        ? "Black Hole" : appName;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string xdotool = ExecuteSystemCommand("xdotool", "getactivewindow getwindowpid");
                    if (int.TryParse(xdotool, out int pid))
                    {
                        return Process.GetProcessById(pid).ProcessName;
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string script = "osascript -e 'tell application \"System Events\" to get name of first process whose frontmost is true'";
                    string appName = ExecuteSystemCommand("bash", $"-c \"{script}\"");
                    if (!string.IsNullOrEmpty(appName)) return appName;
                }
            }
            catch { }
            return "Active App";
        }

        private static string ExecuteSystemCommand(string cmd, string args)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                string res = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return res;
            }
            catch { return ""; }
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemMonitorService.Instance.OnTick -= OnSystemTick;
            }
            catch { }
        }
        #endregion
    }
}

