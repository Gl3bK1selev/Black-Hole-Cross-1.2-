using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Black_Hole_Cross.Services
{
    public static class AutoStartManager
    {
        private const string AppName = "BlackHole";
        private const string WindowsRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Пути для Linux и macOS
        private static readonly string LinuxAutostartDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart");
        private static readonly string LinuxDesktopFilePath = Path.Combine(LinuxAutostartDir, $"{AppName}.desktop");

        private static readonly string MacLaunchAgentsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");
        private static readonly string MacPlistPath = Path.Combine(MacLaunchAgentsDir, $"com.blackhole.cross.plist");

        public static bool IsAutoStartEnabled()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var key = Registry.CurrentUser.OpenSubKey(WindowsRegistryPath);
                    return key?.GetValue(AppName) != null;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return File.Exists(LinuxDesktopFilePath);
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return File.Exists(MacPlistPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Ошибка проверки статуса автозагрузки: {ex.Message}");
            }

            return false;
        }

        public static void EnableAutoStart(EventArgs? eventArgs = null)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    EnableWindows(exePath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    EnableLinux(exePath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    EnableMac(exePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка включения автозагрузки: {ex.Message}");
                throw;
            }
        }

        public static void DisableAutoStart()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    DisableWindows();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    DisableLinux();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    DisableMac();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка отключения автозагрузки: {ex.Message}");
                throw;
            }
        }

        public static string GetAutoStartStatus()
        {
            return IsAutoStartEnabled() ? "Включено" : "Выключено";
        }

        #region Windows
        private static void EnableWindows(string exePath)
        {
            string fullCommand = $"\"{exePath}\" -minimized";
            using var key = Registry.CurrentUser.OpenSubKey(WindowsRegistryPath, true);
            key?.SetValue(AppName, fullCommand);
            Debug.WriteLine("✅ Автозагрузка включена в реестре Windows");
        }

        private static void DisableWindows()
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsRegistryPath, true);
            key?.DeleteValue(AppName, false);
            Debug.WriteLine("✅ Автозагрузка отключена в Windows");
        }
        #endregion

        #region Linux (XDG Autostart)
        private static void EnableLinux(string exePath)
        {
            if (!Directory.Exists(LinuxAutostartDir))
                Directory.CreateDirectory(LinuxAutostartDir);

            string desktopFileContent = $"""
                [Desktop Entry]
                Type=Application
                Name={AppName}
                Exec="{exePath}" -minimized
                Terminal=false
                X-GNOME-Autostart-enabled=true
                """;

            File.WriteAllText(LinuxDesktopFilePath, desktopFileContent);
            Debug.WriteLine($"✅ Автозагрузка включена (XDG Desktop): {LinuxDesktopFilePath}");
        }

        private static void DisableLinux()
        {
            if (File.Exists(LinuxDesktopFilePath))
                File.Delete(LinuxDesktopFilePath);

            Debug.WriteLine("✅ Автозагрузка отключена в Linux");
        }
        #endregion

        #region macOS (LaunchAgents)
        private static void EnableMac(string exePath)
        {
            if (!Directory.Exists(MacLaunchAgentsDir))
                Directory.CreateDirectory(MacLaunchAgentsDir);

            string plistContent = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <String>Label</String>
                    <string>com.blackhole.cross</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exePath}</string>
                        <string>-minimized</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                </dict>
                </plist>
                """;

            File.WriteAllText(MacPlistPath, plistContent);
            Debug.WriteLine($"✅ Автозагрузка включена (macOS LaunchAgent): {MacPlistPath}");
        }

        private static void DisableMac()
        {
            if (File.Exists(MacPlistPath))
                File.Delete(MacPlistPath);

            Debug.WriteLine("✅ Автозагрузка отключена в macOS");
        }
        #endregion
    }
}
