using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Black_Hole_Cross.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Black_Hole_Cross.ControlDiagnostic
{
    public partial class SsdControl : UserControl
    {
        public DirectoryInfo? diskinfo;

       
        private long _lastReadBytes = 0;
        private long _lastWriteBytes = 0;
        private DateTime _lastMetricTime = DateTime.Now;

    
        private PerformanceCounter? readCounter;
        private PerformanceCounter? writeCounter;
        private PerformanceCounter? diskTimeCounter;
        private PerformanceCounter? latencyCounter;

        public SsdControl()
        {
            InitializeComponent();
            InitPerformanceCounters();

            Loaded += SsdControl_Loaded;
            Unloaded += SsdControl_Unloaded;

           
            DiagnosDisk.Text = LocalizationService.Instance["DiskDiagnostics"] ?? "Диагностика дисков";
            CleanCache.Content = LocalizationService.Instance["ClearCache"] ?? "🧹 Очистить кэш";
            ShowBigFiles.Content = LocalizationService.Instance["ShowLargeFiles"] ?? "📂 Показать большие файлы";
        }

        private void InitPerformanceCounters()
        {
           
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                    writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                    diskTimeCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                    latencyCounter = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Transfer", "_Total");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка счетчиков Windows: {ex.Message}");
                }
            }
        }

        private void SsdControl_Loaded(object? sender, RoutedEventArgs e)
        {
            SystemMonitorService.Instance.OnTick += OnSystemTick;
            QuantityDisks();
            UpdatePieChart();
            UpdateDiskMetrics();
        }

        private void SsdControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            SystemMonitorService.Instance.OnTick -= OnSystemTick;

            readCounter?.Dispose();
            writeCounter?.Dispose();
            diskTimeCounter?.Dispose();
            latencyCounter?.Dispose();
        }

        private void OnSystemTick()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    UpdateDiskMetrics();
                    UpdatePieChart();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка обновления данных SSD: {ex.Message}");
                }
            });
        }

     
        private void UpdateDiskMetrics()
        {
            try
            {
                float readSpeed = 0;
                float writeSpeed = 0;
                float diskUsage = 0;
                float latencyMs = 0;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (readCounter != null && writeCounter != null && diskTimeCounter != null && latencyCounter != null)
                    {
                        readSpeed = readCounter.NextValue() / 1024f / 1024f;
                        writeSpeed = writeCounter.NextValue() / 1024f / 1024f;
                        diskUsage = diskTimeCounter.NextValue();
                        latencyMs = latencyCounter.NextValue() * 1000f;
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                   
                    var metrics = GetLinuxDiskMetrics();
                    readSpeed = metrics.readSpeed;
                    writeSpeed = metrics.writeSpeed;
                    diskUsage = metrics.usage;
                    latencyMs = metrics.latency;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    
                    var metrics = GetMacDiskMetrics();
                    readSpeed = metrics.readSpeed;
                    writeSpeed = metrics.writeSpeed;
                }

               
                DiskInfoDisplay.Text = $"⚡ Динамика системы (в реальном времени):\n" +
                                       $"Чтение: {readSpeed:F1} МБ/с | Запись: {writeSpeed:F1} МБ/с\n" +
                                       $"Нагрузка диска: {diskUsage:F0}% | Средний отклик: {latencyMs:F1} мс";
            }
            catch (Exception ex)
            {
                DiskInfoDisplay.Text = "Ошибка чтения метрик: " + ex.Message;
            }
        }

        
        private (float readSpeed, float writeSpeed, float usage, float latency) GetLinuxDiskMetrics()
        {
            try
            {
                if (!File.Exists("/proc/diskstats")) return (0, 0, 0, 0);

                string[] lines = File.ReadAllLines("/proc/diskstats");
                long currentReadBytes = 0;
                long currentWriteBytes = 0;

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 14 && (parts[2].StartsWith("sd") || parts[2].StartsWith("nvme")))
                    {
                        currentReadBytes += long.Parse(parts[5]) * 512; // Секторы чтения * 512
                        currentWriteBytes += long.Parse(parts[9]) * 512; // Секторы записи * 512
                    }
                }

                double elapsedSec = (DateTime.Now - _lastMetricTime).TotalSeconds;
                if (elapsedSec <= 0) elapsedSec = 1;

                float rSpeed = (float)((currentReadBytes - _lastReadBytes) / elapsedSec / (1024 * 1024));
                float wSpeed = (float)((currentWriteBytes - _lastWriteBytes) / elapsedSec / (1024 * 1024));

                _lastReadBytes = currentReadBytes;
                _lastWriteBytes = currentWriteBytes;
                _lastMetricTime = DateTime.Now;

                return (Math.Max(0, rSpeed), Math.Max(0, wSpeed), 0, 0);
            }
            catch { return (0, 0, 0, 0); }
        }

        private (float readSpeed, float writeSpeed) GetMacDiskMetrics()
        {
           
            return (0.0f, 0.0f);
        }

      
        private void UpdatePieChart()
        {
            InfoView.Children.Clear();

            var disksData = GetFullCrossPlatformDisks();

            foreach (var disk in disksData)
            {
                double total = disk.TotalGB;
                double free = disk.FreeGB;
                double used = total - free;
                double usedPercent = total > 0 ? (used / total) * 100 : 0;

                var cardBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(10),
                    Width = 360
                };

                var stack = new StackPanel { Spacing = 6 };

                var infoText = new TextBlock
                {
                    Text = $"💻 Модель: {disk.Model}\n" +
                           $"🔌 Интерфейс: {disk.InterfaceType}\n" +
                           $"📁 Раздел: {disk.Name} ({disk.VolumeLabel})\n" +
                           $"⚙️ Файловая система: {disk.Format}\n" +
                           $"📊 Объем: {total:F1} ГБ | Свободно: {free:F1} ГБ\n" +
                           $"🔥 Занято: {used:F1} ГБ ({usedPercent:F0}%)",
                    FontSize = 13,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                };

                var pieChart = new PieChart
                {
                    Width = 300,
                    Height = 190,
                    Series = new ISeries[]
                    {
                        new PieSeries<double>
                        {
                            Name = "Занято",
                            Values = new double[] { used },
                            Fill = new SolidColorPaint(SKColors.Crimson)
                        },
                        new PieSeries<double>
                        {
                            Name = "Свободно",
                            Values = new double[] { free },
                            Fill = new SolidColorPaint(SKColors.MediumSeaGreen)
                        }
                    }
                };

                stack.Children.Add(infoText);
                stack.Children.Add(pieChart);
                cardBorder.Child = stack;

                InfoView.Children.Add(cardBorder);
            }
        }

        private void CacheClean_Click(object? sender, RoutedEventArgs e)
        {
            string tempPath = Path.GetTempPath();

            if (!Directory.Exists(tempPath))
            {
                ShowInfoMessage("Папка временных файлов не найдена.");
                return;
            }

            List<string> failedDeletes = new List<string>();
            long totalSizeDeleted = 0;

            DirectoryInfo di = new DirectoryInfo(tempPath);

            foreach (FileInfo fi in di.GetFiles())
            {
                try
                {
                    totalSizeDeleted += fi.Length;
                    fi.Delete();
                }
                catch (Exception ex)
                {
                    failedDeletes.Add($"{fi.FullName} — {ex.Message}");
                }
            }

            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                try
                {
                    long dirSize = GetDirectorySize(dir);
                    totalSizeDeleted += dirSize;
                    dir.Delete(true);
                }
                catch (Exception ex)
                {
                    failedDeletes.Add($"{dir.FullName} — {ex.Message}");
                }
            }

            string freedSpace = FormatSize(totalSizeDeleted);
            if (failedDeletes.Count == 0)
            {
                ShowInfoMessage($"✅ Очистка завершена! Освобождено {freedSpace}.");
            }
            else
            {
                ShowInfoMessage($"Очистка завершена. Освобождено {freedSpace}.\n(Некоторые файлы заняты другой программой)");
            }
        }

        private long GetDirectorySize(DirectoryInfo dir)
        {
            long size = 0;
            try
            {
                foreach (FileInfo file in dir.GetFiles()) size += file.Length;
                foreach (DirectoryInfo subDir in dir.GetDirectories()) size += GetDirectorySize(subDir);
            }
            catch { }
            return size;
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F2} MB";
            return $"{bytes / 1073741824.0:F2} GB";
        }

        private void QuantityDisks()
        {
            Disk.Items.Clear();
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.IsReady)
                    {
                        Disk.Items.Add($"{drive.Name} [{drive.DriveFormat}]");
                    }
                }
                if (Disk.Items.Count > 0) Disk.SelectedIndex = 0;
            }
            catch { }
        }

        private void ComboBox_SelectedChanged(object? s, SelectionChangedEventArgs args)
        {
            if (Disk.SelectedItem != null)
            {
                string selectedDriveRaw = Disk.SelectedItem.ToString()!;
                string drivePath = selectedDriveRaw.Split(' ')[0];
                diskinfo = new DirectoryInfo(drivePath);
            }
        }

        private async void ShowLargestFiles_Click(object? s, RoutedEventArgs args)
        {
            if (diskinfo == null) return;

            try
            {
                var sortedFiles = await Task.Run(() => GetLargeFiles(diskinfo.FullName));

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🔝 ТОП-50 тяжелых файлов на {diskinfo.Name}:");
                sb.AppendLine("──────────────────────────────────");

                int i = 1;
                foreach (var file in sortedFiles)
                {
                    sb.AppendLine($"{i++}. {file.Name} — {FormatFileSize(file.Length)}");
                    sb.AppendLine($" 📂 {file.FullName}");
                    sb.AppendLine();
                }

                Window resultWindow = new Window
                {
                    Title = "Результаты поиска файлов",
                    Width = 650,
                    Height = 480,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                TextBox tb = new TextBox
                {
                    Text = sb.ToString(),
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10),
                    FontFamily = new FontFamily("Consolas")
                };

                resultWindow.Content = tb;

                if (VisualRoot is Window parentWindow)
                {
                    await resultWindow.ShowDialog(parentWindow);
                }
                else
                {
                    resultWindow.Show();
                }
            }
            catch (Exception ex)
            {
                ShowInfoMessage($"Ошибка сканирования: {ex.Message}");
            }
        }

        private List<FileInfo> GetLargeFiles(string rootPath)
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
                MaxRecursionDepth = 50
            };

            DirectoryInfo di = new DirectoryInfo(rootPath);

            return di.EnumerateFiles("*", options)
                     .OrderByDescending(f => f.Length)
                     .Take(50)
                     .ToList();
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        private async void ShowInfoMessage(string message)
        {
            Window dialog = new Window
            {
                Title = "Black Hole Diagnostics",
                Width = 420,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Content = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    Margin = new Thickness(20),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                }
            };

            if (VisualRoot is Window parentWindow)
            {
                await dialog.ShowDialog(parentWindow);
            }
            else
            {
                dialog.Show();
            }
        }

      
        public class FullDiskDetails
        {
            public string Name { get; set; } = "—";
            public string Model { get; set; } = "Накопитель";
            public string InterfaceType { get; set; } = "NVMe / SATA";
            public string VolumeLabel { get; set; } = "Локальный том";
            public string Format { get; set; } = "—";
            public double TotalGB { get; set; }
            public double FreeGB { get; set; }
        }

        public static List<FullDiskDetails> GetFullCrossPlatformDisks()
        {
            var list = new List<FullDiskDetails>();
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var d in drives)
                {
                    if (d.IsReady)
                    {
                        var info = new FullDiskDetails
                        {
                            Name = d.Name,
                            Format = d.DriveFormat,
                            VolumeLabel = string.IsNullOrEmpty(d.VolumeLabel) ? "Локальный диск" : d.VolumeLabel,
                            TotalGB = d.TotalSize / (1024.0 * 1024 * 1024),
                            FreeGB = d.TotalFreeSpace / (1024.0 * 1024 * 1024)
                        };

                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            info.Model = $"Disk ({d.Name.TrimEnd('\\')})";
                            info.InterfaceType = "SATA/NVMe";
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            info.Model = "Linux Storage Device";
                            info.InterfaceType = "Block Device";
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            info.Model = "Apple Storage Container";
                            info.InterfaceType = "PCIe / NVMe";
                        }

                        list.Add(info);
                    }
                }
            }
            catch { }
            return list;
        }
    }
}
