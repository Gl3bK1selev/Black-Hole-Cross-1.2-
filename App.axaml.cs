using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Black_Hole_Cross.Services;
using Black_Hole_Cross.Views;
using BlackHole;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Black_Hole_Cross;

public partial class App : Application
{
    static App()
    {
        LoadOpenAL();
    }

    private static void LoadOpenAL()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(baseDir, "OpenAL32.dll");

            if (File.Exists(dllPath))
            {
                NativeLibrary.Load(dllPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка загрузки OpenAL: {ex.Message}");
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        
            
            var restoreItem = new NativeMenuItem("Восстановить");
            restoreItem.Click += OnRestoreMenuItemClick;

            var exitItem = new NativeMenuItem("Выход");
            exitItem.Click += OnExitMenuItemClick;

            var nativeMenu = new NativeMenu();
            nativeMenu.Items.Add(restoreItem);
            nativeMenu.Items.Add(new NativeMenuItemSeparator());
            nativeMenu.Items.Add(exitItem);

        
            var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
            if (trayIcon != null)
            {
                trayIcon.Menu = nativeMenu;
            }
        

        base.OnFrameworkInitializationCompleted();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) LogUnhandledException(ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogUnhandledException(args.Exception);
            args.SetObserved();
        };

        SimpleSettings.LoadSettings();
        LocalizationService.Instance.SetLanguage(SimpleSettings.Language);
        ApplyInitialTheme(SimpleSettings.SelectedTheme);

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

      
        if (SimpleSettings.RunAsAdmin && !IsRunAsAdmin())
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(processInfo);
                desktop.Shutdown();
                return;
            }
            catch { }
        }

     
        RealSystemMonitor.Instance.OnNotification += note =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var win = new NotificationWindow();
                win.ShowNotification(note);
            });
        };

        if (SimpleSettings.EnableNotifications)
            RealSystemMonitor.Instance.StartMonitoring();

     
        bool isAutoStart = desktop.Args != null && desktop.Args.Any(arg => arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

        if (isAutoStart)
        {
           
            var splash = new MainWindow(); 
            desktop.MainWindow = splash;
            splash.Show();
        }
        else if (!SimpleSettings.OnboardingCompleted)
        {
          
            var onboarding = new OnboardingWindow();
            desktop.MainWindow = onboarding;
            onboarding.Show();
        }
        else
        {
          
            var managerMenu = new ManagerMenu();
            desktop.MainWindow = managerMenu;
            managerMenu.Show();
        }
    }

    private void ApplyInitialTheme(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName)) return;

        try
        {
            var themeUri = new Uri($"avares://Black_Hole_Cross/Themes/{themeName}.axaml");
            if (Avalonia.Platform.AssetLoader.Exists(themeUri))
            {
                var newThemeDict = (ResourceDictionary)AvaloniaXamlLoader.Load(themeUri);
                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(newThemeDict);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Ошибка темы {themeName}: {ex.Message}");
        }
    }

    private static void LogUnhandledException(Exception ex)
    {
        try
        {
            File.WriteAllText("error.txt", $"СООБЩЕНИЕ: {ex.Message}\nГДЕ: {ex.StackTrace}");
        }
        catch { }
    }

    private static bool IsRunAsAdmin()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    private void OnRestoreMenuItemClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            desktop.MainWindow.Show();
            desktop.MainWindow.WindowState = WindowState.Normal;
            desktop.MainWindow.Activate();
        }
    }

    private void OnExitMenuItemClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

}
