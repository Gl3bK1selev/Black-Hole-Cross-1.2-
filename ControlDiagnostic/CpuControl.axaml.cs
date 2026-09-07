using Avalonia.Controls;
using Avalonia.Interactivity;
using Black_Hole_Cross.Services;
using LibreHardwareMonitor.Hardware;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Black_Hole_Cross.ControlDiagnostic
{
    public partial class CpuControl : UserControl
    {
        private bool _isPaused = false;

        // Windows PerformanceCounters
        private PerformanceCounter? _totalCpuCounter;
        private PerformanceCounter? _freqCounter;
        private PerformanceCounter[]? _coreCounters;

        // Hardware Monitor
        private Computer? _computer;
        private uint _maxClockSpeed = 0;

      
        private long[]? _prevIdleTime;
        private long[]? _prevTotalTime;

        // Данные для графиков
        private ObservableCollection<double> _cpuValues = new();
        private ObservableCollection<double> _freqValues = new();
        private ObservableCollection<double> _tempValues = new();
        private ObservableCollection<double>? _voltageValues = new();
        private ObservableCollection<double>? _powerValues = new();
        private ObservableCollection<double>[]? _coreValuesArray;

        private double _cachedTemp = 0;
        private double _cachedVoltage = 0;
        private double _cachedPower = 0;
        private DateTime _lastSensorsUpdate = DateTime.MinValue;
        private bool _isInitialized = false;

        public CpuControl()
        {
            InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            UpdateTexts();

            this.Loaded += OnControlLoaded;
            this.Unloaded += OnControlUnloaded;
        }

        private async void OnControlLoaded(object? sender, RoutedEventArgs e)
        {
          
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                await LoadStaticCpuInfoAsync();
                await Task.Run(() => InitializePerformanceCounters());
                SetupCharts();

              
                if (SystemMonitorService.Instance != null)
                {
                    SystemMonitorService.Instance.OnTick += OnSystemTick;
                }
                else
                {
                    Debug.WriteLine("[CpuControl] SystemMonitorService.Instance is null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CpuControl Init Error]: {ex.Message}");
                if (CpuInfo != null)
                    CpuInfo.Text = $"Ошибка инициализации: {ex.Message}";
            }
        }

        private async Task LoadStaticCpuInfoAsync()
        {
            try
            {
                string info = await GetStaticCpuInfoAsync();
                if (CpuInfo != null)
                    CpuInfo.Text = info;
            }
            catch (Exception ex)
            {
                if (CpuInfo != null)
                    CpuInfo.Text = $"Не удалось загрузить данные CPU: {ex.Message}";
            }
        }

        private void InitializePerformanceCounters()
        {
            int coreCount = Environment.ProcessorCount;
            _prevIdleTime = new long[coreCount + 1];
            _prevTotalTime = new long[coreCount + 1];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _totalCpuCounter.NextValue();

                    _freqCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                    _freqCounter.NextValue();

                    _coreCounters = new PerformanceCounter[coreCount];
                    for (int i = 0; i < coreCount; i++)
                    {
                        _coreCounters[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                        _coreCounters[i].NextValue();
                    }

                    try
                    {
                        using (var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                _maxClockSpeed = (uint)obj["MaxClockSpeed"];
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Если WMI не доступен, используем запасное значение
                        _maxClockSpeed = 3000; 
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Windows Counters Fail]: {ex.Message}");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    if (File.Exists("/proc/cpuinfo"))
                    {
                        var lines = File.ReadAllLines("/proc/cpuinfo");
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("cpu MHz"))
                            {
                                var parts = line.Split(':');
                                if (parts.Length > 1 && double.TryParse(parts[1].Trim(), out double mhz))
                                {
                                    _maxClockSpeed = (uint)(mhz);
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    string result = ExecuteCommand("sysctl", "-n hw.cpufrequency");
                    if (double.TryParse(result, out double hz))
                    {
                        _maxClockSpeed = (uint)(hz / 1000000.0);
                    }
                }
                catch { }
            }

            // Инициализация LibreHardwareMonitor
            try
            {
                _computer = new Computer { IsCpuEnabled = true, IsMotherboardEnabled = true };
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibreHardwareMonitor Init Error]: {ex.Message}");
            }
        }

        private void OnSystemTick()
        {
            if (_isPaused) return;

            try
            {
                double cpuLoad = ReadTotalCpuLoad();
                double currentFreq = ReadCpuFrequency();
                double[] coreLoads = ReadCoreLoads();

                ReadSensorsRealtime(out double temp, out double voltage, out double power);

                UpdateAllCharts(cpuLoad, currentFreq, coreLoads, temp, voltage, power);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tick Error]: {ex.Message}");
            }
        }

   
        private double ReadTotalCpuLoad()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _totalCpuCounter != null)
            {
                try { return Math.Round(_totalCpuCounter.NextValue(), 1); }
                catch { return 0; }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxCpuLoadTotal();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacCpuLoadTotal();
            }
            return 0;
        }

        private double ReadCpuFrequency()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _freqCounter != null && _maxClockSpeed > 0)
            {
                try
                {
                    double freq = _freqCounter.NextValue();
                    return Math.Round(_maxClockSpeed * (freq / 100.0), 0);
                }
                catch { return _maxClockSpeed; }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    if (File.Exists("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"))
                    {
                        string val = File.ReadAllText("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq").Trim();
                        return Math.Round(double.Parse(val) / 1000.0, 0);
                    }
                }
                catch { }

            
                try
                {
                    if (File.Exists("/proc/cpuinfo"))
                    {
                        var lines = File.ReadAllLines("/proc/cpuinfo");
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("cpu MHz"))
                            {
                                var parts = line.Split(':');
                                if (parts.Length > 1 && double.TryParse(parts[1].Trim(), out double mhz))
                                {
                                    return Math.Round(mhz, 0);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string res = ExecuteCommand("sysctl", "-n hw.cpufrequency");
                if (double.TryParse(res, out double hz))
                    return Math.Round(hz / 1000000.0, 0);
            }
            return _maxClockSpeed > 0 ? _maxClockSpeed : 3000;
        }

        private double[] ReadCoreLoads()
        {
            int coreCount = Environment.ProcessorCount;
            double[] loads = new double[coreCount];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _coreCounters != null)
            {
                for (int i = 0; i < Math.Min(coreCount, _coreCounters.Length); i++)
                {
                    try { loads[i] = Math.Round(_coreCounters[i].NextValue(), 1); }
                    catch { loads[i] = 0; }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && _prevIdleTime != null && _prevTotalTime != null)
            {
                loads = GetLinuxCoreLoads();
            }

            return loads;
        }

        private void ReadSensorsRealtime(out double temp, out double voltage, out double power)
        {
            if ((DateTime.Now - _lastSensorsUpdate).TotalSeconds < 2)
            {
                temp = _cachedTemp;
                voltage = _cachedVoltage;
                power = _cachedPower;
                return;
            }

            temp = 0; voltage = 0; power = 0;

            if (_computer != null)
            {
                try
                {
                    _computer.Accept(new FastUpdateVisitor());
                    foreach (var hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            foreach (var sensor in hw.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && temp == 0)
                                    temp = sensor.Value.Value;
                                else if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue && voltage == 0)
                                    voltage = sensor.Value.Value;
                                else if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && power == 0)
                                    power = sensor.Value.Value;
                            }
                        }
                    }
                }
                catch { }
            }

        
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (temp == 0 && File.Exists("/sys/class/thermal/thermal_zone0/temp"))
                {
                    try
                    {
                        string raw = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp").Trim();
                        if (double.TryParse(raw, out double tRaw))
                            temp = tRaw > 1000 ? tRaw / 1000.0 : tRaw;
                    }
                    catch { }
                }

                // Попытка получить температуру из других зон
                if (temp == 0)
                {
                    try
                    {
                        var zones = Directory.GetDirectories("/sys/class/thermal", "thermal_zone*");
                        foreach (var zone in zones)
                        {
                            string typePath = Path.Combine(zone, "type");
                            if (File.Exists(typePath))
                            {
                                string type = File.ReadAllText(typePath).Trim();
                                if (type.Contains("cpu") || type.Contains("x86"))
                                {
                                    string tempPath = Path.Combine(zone, "temp");
                                    if (File.Exists(tempPath))
                                    {
                                        string raw = File.ReadAllText(tempPath).Trim();
                                        if (double.TryParse(raw, out double tRaw))
                                        {
                                            temp = tRaw > 1000 ? tRaw / 1000.0 : tRaw;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            _cachedTemp = temp;
            _cachedVoltage = voltage;
            _cachedPower = power;
            _lastSensorsUpdate = DateTime.Now;
        }

    
        private double GetLinuxCpuLoadTotal()
        {
            try
            {
                if (!File.Exists("/proc/stat")) return 0;
                string line = File.ReadAllLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
                if (line == null) return 0;

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 8) return 0;

                long idle = long.Parse(parts[4]) + long.Parse(parts[5]);
                long total = parts.Skip(1).Take(7).Sum(p => long.Parse(p));

                if (_prevIdleTime == null || _prevTotalTime == null) return 0;

                long dIdle = idle - _prevIdleTime[0];
                long dTotal = total - _prevTotalTime[0];

                _prevIdleTime[0] = idle;
                _prevTotalTime[0] = total;

                return dTotal == 0 ? 0 : Math.Round((1.0 - (double)dIdle / dTotal) * 100.0, 1);
            }
            catch { return 0; }
        }

        private double[] GetLinuxCoreLoads()
        {
            int coreCount = Environment.ProcessorCount;
            double[] loads = new double[coreCount];

            if (_prevIdleTime == null || _prevTotalTime == null) return loads;

            try
            {
                if (!File.Exists("/proc/stat")) return loads;
                var lines = File.ReadAllLines("/proc/stat").Where(l => l.StartsWith("cpu") && l != "cpu ").ToList();

                for (int i = 0; i < Math.Min(coreCount, lines.Count); i++)
                {
                    string[] parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 8) continue;

                    long idle = long.Parse(parts[4]) + long.Parse(parts[5]);
                    long total = parts.Skip(1).Take(7).Sum(p => long.Parse(p));

                    long dIdle = idle - _prevIdleTime[i + 1];
                    long dTotal = total - _prevTotalTime[i + 1];

                    _prevIdleTime[i + 1] = idle;
                    _prevTotalTime[i + 1] = total;

                    loads[i] = dTotal == 0 ? 0 : Math.Round((1.0 - (double)dIdle / dTotal) * 100.0, 1);
                }
            }
            catch { }
            return loads;
        }

        private double GetMacCpuLoadTotal()
        {
            try
            {
                string output = ExecuteCommand("top", "-l 1 -n 0");
                var line = output.Split('\n').FirstOrDefault(l => l.Contains("CPU usage"));
                if (line != null)
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2)
                    {
                        var userPart = parts[0].Split(':')[1].Replace("%", "").Trim();
                        var sysPart = parts[1].Replace("%", "").Trim();
                        if (double.TryParse(userPart, out double user) && double.TryParse(sysPart, out double sys))
                            return Math.Round(user + sys, 1);
                    }
                }
            }
            catch { }
            return 0;
        }

      

        private async Task<string> GetStaticCpuInfoAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();

                try
                {
                    sb.AppendLine($"🔸 Запущенных процессов в ОС: {Process.GetProcesses().Length}");
                    int totalThreads = 0;
                    foreach (var p in Process.GetProcesses())
                    {
                        try { totalThreads += p.Threads.Count; } catch { }
                    }
                    sb.AppendLine($"🔸 Всего системных потоков: {totalThreads}");
                    sb.AppendLine($"🔸 Разрядность процессора: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
                    sb.AppendLine("─".PadRight(45, '─'));

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        GetWindowsExtendedInfo(sb);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        GetLinuxExtendedInfo(sb);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        GetMacExtendedInfo(sb);
                    }

                    GetSupportedInstructions(sb);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Ошибка получения информации: {ex.Message}");
                }

                return sb.ToString();
            });
        }

        private void GetWindowsExtendedInfo(StringBuilder sb)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        sb.AppendLine($"💻 Процессор: {obj["Name"]?.ToString()?.Trim() ?? "Неизвестно"}");
                        sb.AppendLine($"🔸 Производитель: {obj["Manufacturer"]?.ToString() ?? "Неизвестно"}");
                        sb.AppendLine($"🔸 Сокет: {obj["SocketDesignation"]?.ToString() ?? "Неизвестно"}");
                        sb.AppendLine($"🔸 Ядер / Потоков: {obj["NumberOfCores"]}C / {obj["NumberOfLogicalProcessors"]}T");
                        sb.AppendLine($"🔸 Степпинг / Ревизия: {obj["Revision"]}");
                        sb.AppendLine($"🔸 Базовая частота: {SafeGetInt(obj, "MaxClockSpeed")} МГц");
                        sb.AppendLine($"🔸 Частота шины (BCLK): {obj["ExtClock"]} МГц");
                        sb.AppendLine($"🔸 Кэш L2: {obj["L2CacheSize"]} КБ");
                        sb.AppendLine($"🔸 Кэш L3: {obj["L3CacheSize"]} КБ");
                        if (obj["VirtualizationFirmwareEnabled"] != null)
                            sb.AppendLine($"🔸 Виртуализация (BIOS): {((bool)obj["VirtualizationFirmwareEnabled"] ? "Включена" : "Выключена")}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ WMI Error: {ex.Message}");
            }
        }

        private void GetLinuxExtendedInfo(StringBuilder sb)
        {
            try
            {
                if (File.Exists("/proc/cpuinfo"))
                {
                    var lines = File.ReadAllLines("/proc/cpuinfo");
                    foreach (var l in lines)
                    {
                        if (l.StartsWith("model name"))
                        {
                            sb.AppendLine($"💻 Процессор: {l.Split(':')[1].Trim()}");
                            break;
                        }
                    }
                    foreach (var l in lines)
                    {
                        if (l.StartsWith("vendor_id"))
                        {
                            sb.AppendLine($"🔸 Производитель: {l.Split(':')[1].Trim()}");
                            break;
                        }
                    }
                    sb.AppendLine($"🔸 Логических потоков: {Environment.ProcessorCount}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Linux Info Error: {ex.Message}");
            }
        }

        private void GetMacExtendedInfo(StringBuilder sb)
        {
            try
            {
                sb.AppendLine($"💻 Процессор: {ExecuteCommand("sysctl", "-n machdep.cpu.brand_string")}");
                sb.AppendLine($"🔸 Физических ядер: {ExecuteCommand("sysctl", "-n hw.physicalcpu")}");
                sb.AppendLine($"🔸 Логических потоков: {ExecuteCommand("sysctl", "-n hw.logicalcpu")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Mac Info Error: {ex.Message}");
            }
        }

        private void GetSupportedInstructions(StringBuilder sb)
        {
            sb.AppendLine("\n⚡ Инструкции и расширения:");
            try
            {
                if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported) sb.AppendLine(" ✅ AVX-512");
                if (System.Runtime.Intrinsics.X86.Avx2.IsSupported) sb.AppendLine(" ✅ AVX2");
                if (System.Runtime.Intrinsics.X86.Avx.IsSupported) sb.AppendLine(" ✅ AVX");
                if (System.Runtime.Intrinsics.X86.Aes.IsSupported) sb.AppendLine(" ✅ AES-NI");
                if (System.Runtime.Intrinsics.X86.Sse42.IsSupported) sb.AppendLine(" ✅ SSE4.2");
                if (System.Runtime.Intrinsics.Arm.ArmBase.IsSupported) sb.AppendLine(" ✅ ARM64 Base Vector");
            }
            catch { }
        }

      
        private void SetupCharts()
        {
            try
            {
                int coreCount = Environment.ProcessorCount;

                _cpuValues = new ObservableCollection<double>();
                if (CPUChart != null)
                {
                    CPUChart.Series = new ISeries[] { CreateSeries("CPU %", _cpuValues, SKColors.SpringGreen) };
                }

                _freqValues = new ObservableCollection<double>();
                if (CPUChartFrequency != null)
                {
                    CPUChartFrequency.Series = new ISeries[] { CreateSeries("МГц", _freqValues, SKColors.Orange) };
                }

                _tempValues = new ObservableCollection<double>();
                if (CPUChartTemp != null)
                {
                    CPUChartTemp.Series = new ISeries[] { CreateSeries("°C", _tempValues, SKColors.Crimson) };
                }

                if (CPUChartVoltage != null)
                {
                    _voltageValues = new ObservableCollection<double>();
                    CPUChartVoltage.Series = new ISeries[] { CreateSeries("Вольт", _voltageValues, SKColors.DeepSkyBlue) };
                }

                if (CPUChartPower != null)
                {
                    _powerValues = new ObservableCollection<double>();
                    CPUChartPower.Series = new ISeries[] { CreateSeries("Ватт", _powerValues, SKColors.MediumPurple) };
                }

                if (CPUChartCore != null)
                {
                    var coreSeries = new ISeries[coreCount];
                    _coreValuesArray = new ObservableCollection<double>[coreCount];

                    for (int i = 0; i < coreCount; i++)
                    {
                        _coreValuesArray[i] = new ObservableCollection<double>();
                        coreSeries[i] = CreateSeries($"Ядро {i + 1}", _coreValuesArray[i], SKColors.DodgerBlue);
                    }
                    CPUChartCore.Series = coreSeries;
                }

             
                for (int i = 0; i < 10; i++)
                {
                    _cpuValues.Add(0);
                    _freqValues.Add(0);
                    _tempValues.Add(0);
                    _voltageValues?.Add(0);
                    _powerValues?.Add(0);
                    if (_coreValuesArray != null)
                    {
                        foreach (var core in _coreValuesArray) core?.Add(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SetupCharts Error]: {ex.Message}");
            }
        }

        private LineSeries<double> CreateSeries(string name, ObservableCollection<double> values, SKColor color)
        {
            return new LineSeries<double>
            {
                Name = name,
                Values = values,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(color, 2),
                Fill = null
            };
        }

        private void UpdateAllCharts(double cpuLoad, double frequency, double[] coreLoads, double temp, double voltage, double power)
        {
            try
            {
                const int maxHistory = 60;

                UpdateChartSeries(_cpuValues, cpuLoad, maxHistory);
                UpdateChartSeries(_freqValues, frequency, maxHistory);
                if (temp > 0) UpdateChartSeries(_tempValues, temp, maxHistory);
                if (voltage > 0 && _voltageValues != null) UpdateChartSeries(_voltageValues, voltage, maxHistory);
                if (power > 0 && _powerValues != null) UpdateChartSeries(_powerValues, power, maxHistory);

                if (coreLoads != null && _coreValuesArray != null)
                {
                    for (int i = 0; i < Math.Min(coreLoads.Length, _coreValuesArray.Length); i++)
                    {
                        if (_coreValuesArray[i] != null)
                            UpdateChartSeries(_coreValuesArray[i], coreLoads[i], maxHistory);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateCharts Error]: {ex.Message}");
            }
        }

        private void UpdateChartSeries(ObservableCollection<double> series, double newValue, int maxSize)
        {
            if (series == null) return;
            try
            {
                series.Add(newValue);
                if (series.Count > maxSize) series.RemoveAt(0);
            }
            catch { }
        }

        private string ExecuteCommand(string cmd, string args)
        {
            try
            {
                var p = new Process
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
                p.WaitForExit(1000);
                return res;
            }
            catch { return ""; }
        }

        private int SafeGetInt(ManagementObject obj, string prop)
        {
            try { return obj[prop] != null ? Convert.ToInt32(obj[prop]) : 0; }
            catch { return 0; }
        }

        private void OnControlUnloaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
                if (SystemMonitorService.Instance != null)
                {
                    SystemMonitorService.Instance.OnTick -= OnSystemTick;
                }

                _totalCpuCounter?.Dispose();
                _freqCounter?.Dispose();
                if (_coreCounters != null)
                {
                    foreach (var c in _coreCounters) c?.Dispose();
                }

                _computer?.Close();
                _computer = null;
            }
            catch { }
        }

        private void PauseButton_Click(object? sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            if (PauseButton != null)
                PauseButton.Content = _isPaused ? "▶ Запуск" : "⏸ Пауза";
        }

        private void OnLanguageChanged() => UpdateTexts();
        private void UpdateTexts() { }

        private class FastUpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                foreach (var h in computer.Hardware)
                    h.Accept(this);
            }
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }
    }
}