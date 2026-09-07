using Avalonia.Threading;
using Black_Hole_Cross.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Black_Hole_Cross
{
    public static class SimpleSettings
    {
       
        private static readonly string SettingsFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BlackHoleCross");

        private static readonly string SettingsFilePath = Path.Combine(SettingsFolderPath, "app_settings.json");

        // --- Основные настройки ---
        public static string Language { get; set; } = "ru";
        public static string UserName { get; set; } = "";
        public static string UserEmail { get; set; } = "";
        public static DateTime UserBirthday { get; set; } = DateTime.MinValue;
        public static bool OnboardingCompleted { get; set; } = false;

        // --- Статистика использования ---
        public static int LaunchCount { get; set; } = 0;
        public static DateTime FirstLaunchDate { get; set; } = DateTime.MinValue;
        public static double TotalUsageMinutes { get; set; } = 0;

        // --- Анимации и уведомления ---
        public static bool EnableSeasonalAnimations { get; set; } = true;
        public static bool EnableStarAnimations { get; set; } = true;
        public static bool EnableNotifications { get; set; } = true;
        public static DateTime LastDailyReport { get; set; } = DateTime.MinValue;

        // --- Система и Производительность ---
        public static bool AutoStart { get; set; } = false;
        public static bool MinimizeToTray { get; set; } = true;
        public static bool HardwareMonitoring { get; set; } = true;
        public static bool PerformanceMode { get; set; } = false;
        public static int UpdateInterval { get; set; } = 5;
        public static bool RunAsAdmin { get; set; } = false;

        // --- Настройки дисплея ---
        public static string DisplayMonitor { get; set; } = "";
        public static string DisplayResolution { get; set; } = "";
        public static int DisplayScaling { get; set; } = 100;
        public static string WindowMode { get; set; } = "";

        // --- Настройки темы ---
        public static string SelectedTheme { get; set; } = "";

        public static event Action? SettingsChanged;

        // --- Методы обновления настроек ---

        public static void SaveAutoStart(bool enabled)
        {
            AutoStart = enabled;
            try
            {
                if (enabled)
                    AutoStartManager.EnableAutoStart();
                else
                    AutoStartManager.DisableAutoStart();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Не удалось применить автозагрузку в системе: {ex.Message}");
            }

            SaveSettings();
        }

        public static void SaveBirthday(DateTime birthday) { UserBirthday = birthday; SaveSettings(); }
        public static void SaveUsageStats(int launchCount, DateTime firstLaunch, double totalMinutes)
        {
            LaunchCount = launchCount;
            FirstLaunchDate = firstLaunch;
            TotalUsageMinutes = totalMinutes;
            SaveSettings();
        }

        public static void SaveDisplaySettings(string monitor, string resolution, string scaling, string windowMode)
        {
            DisplayMonitor = monitor;
            DisplayResolution = resolution;
            if (int.TryParse(scaling?.Replace("%", ""), out int scalingValue))
                DisplayScaling = scalingValue;
            WindowMode = windowMode;
            SaveSettings();
        }

        public static void SaveDisplayMonitor(string monitor) { DisplayMonitor = monitor; SaveSettings(); }
        public static void SaveDisplayResolution(string resolution) { DisplayResolution = resolution; SaveSettings(); }
        public static void SaveDisplayScaling(int scaling) { DisplayScaling = scaling; SaveSettings(); }
        public static void SaveWindowMode(string mode) { WindowMode = mode; SaveSettings(); }
        public static void SaveLanguage(string lang) { Language = lang; SaveSettings(); }
        public static void SaveNotifications(bool enable) { EnableNotifications = enable; SaveSettings(); }
        public static void SaveUpdateInterval(int interval) { UpdateInterval = interval; SaveSettings(); }
        public static void SaveRunAsAdmin(bool enabled) { RunAsAdmin = enabled; SaveSettings(); }
        public static void SaveEmail(string email) { UserEmail = email; SaveSettings(); }
        public static void SaveTheme(string theme) { SelectedTheme = theme; SaveSettings(); }

        // --- Ядро: Сохранение и Загрузка ---

        public static void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(SettingsFolderPath))
                    Directory.CreateDirectory(SettingsFolderPath);

                var data = new SettingsData
                {
                    Language = Language,
                    UserName = UserName,
                    UserEmail = UserEmail,
                    UserBirthday = UserBirthday,
                    LaunchCount = LaunchCount,
                    FirstLaunchDate = FirstLaunchDate,
                    TotalUsageMinutes = TotalUsageMinutes,
                    OnboardingCompleted = OnboardingCompleted,
                    EnableSeasonalAnimations = EnableSeasonalAnimations,
                    EnableStarAnimations = EnableStarAnimations,
                    EnableNotifications = EnableNotifications,
                    LastDailyReport = LastDailyReport,
                    AutoStart = AutoStart,
                    MinimizeToTray = MinimizeToTray,
                    HardwareMonitoring = HardwareMonitoring,
                    PerformanceMode = PerformanceMode,
                    UpdateInterval = UpdateInterval,
                    RunAsAdmin = RunAsAdmin,
                    DisplayMonitor = DisplayMonitor,
                    DisplayResolution = DisplayResolution,
                    DisplayScaling = DisplayScaling,
                    WindowMode = WindowMode,
                    SelectedTheme = SelectedTheme
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

                
                string tempPath = SettingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsFilePath, overwrite: true);

                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка сохранения настроек: {ex.Message}");
            }
        }

        public static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath)) return;

                string json = File.ReadAllText(SettingsFilePath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);

                if (data == null) return;

                Language = data.Language;
                UserName = data.UserName;
                UserEmail = data.UserEmail;
                UserBirthday = data.UserBirthday;
                LaunchCount = data.LaunchCount;
                FirstLaunchDate = data.FirstLaunchDate;
                TotalUsageMinutes = data.TotalUsageMinutes;
                OnboardingCompleted = data.OnboardingCompleted;
                EnableSeasonalAnimations = data.EnableSeasonalAnimations;
                EnableStarAnimations = data.EnableStarAnimations;
                EnableNotifications = data.EnableNotifications;
                LastDailyReport = data.LastDailyReport;
                AutoStart = data.AutoStart;
                MinimizeToTray = data.MinimizeToTray;
                HardwareMonitoring = data.HardwareMonitoring;
                PerformanceMode = data.PerformanceMode;
                UpdateInterval = data.UpdateInterval;
                RunAsAdmin = data.RunAsAdmin;
                DisplayMonitor = data.DisplayMonitor ?? "";
                DisplayResolution = data.DisplayResolution ?? "";
                DisplayScaling = data.DisplayScaling > 0 ? data.DisplayScaling : 100;
                WindowMode = data.WindowMode ?? "";
                SelectedTheme = data.SelectedTheme ?? "";

                ThemeService.ChangeTheme(SelectedTheme);
               
                AutoStart = AutoStartManager.IsAutoStartEnabled();
                ApplyAllSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка загрузки настроек: {ex.Message}");
            }
        }
        public static void ApplyAllSettings()
        {
            try
            {
               
                if (!string.IsNullOrEmpty(Language))
                    LocalizationService.Instance.SetLanguage(Language);

                
                if (!string.IsNullOrEmpty(SelectedTheme))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ThemeService.ChangeTheme(SelectedTheme);
                    });
                }

               
                if (EnableNotifications)
                    RealSystemMonitor.Instance.StartMonitoring();
                else
                    RealSystemMonitor.Instance.StopMonitoring();

              
                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка применения настроек: {ex.Message}");
            }
        }


        private class SettingsData
        {
            public string Language { get; set; } = "ru";
            public string UserName { get; set; } = "";
            public string UserEmail { get; set; } = "";
            public DateTime UserBirthday { get; set; } = DateTime.MinValue;
            public int LaunchCount { get; set; } = 0;
            public DateTime FirstLaunchDate { get; set; } = DateTime.MinValue;
            public double TotalUsageMinutes { get; set; } = 0;
            public bool OnboardingCompleted { get; set; } = false;
            public bool EnableSeasonalAnimations { get; set; } = true;
            public bool EnableStarAnimations { get; set; } = true;
            public bool EnableNotifications { get; set; } = true;
            public DateTime LastDailyReport { get; set; } = DateTime.MinValue;
            public bool AutoStart { get; set; } = false;
            public bool MinimizeToTray { get; set; } = true;
            public bool HardwareMonitoring { get; set; } = true;
            public bool PerformanceMode { get; set; } = false;
            public int UpdateInterval { get; set; } = 5;
            public bool RunAsAdmin { get; set; } = false;
            public string DisplayMonitor { get; set; } = "";
            public string DisplayResolution { get; set; } = "";
            public int DisplayScaling { get; set; } = 100;
            public string WindowMode { get; set; } = "";
            public string SelectedTheme { get; set; } = "";
        }
    }
}
