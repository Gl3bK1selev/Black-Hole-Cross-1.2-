using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Black_Hole_Cross;
using Black_Hole_Cross.Panels;
using Black_Hole_Cross.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlackHole;

public partial class ManagerMenu : Window
{
    public static ManagerMenu? Instance { get; private set; }
    private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_config.txt");

    public ManagerMenu()
    {
        InitializeComponent();
        Instance = this;

        ApplyThemeDirectly(LoadTheme());

        

        if (MenuList != null)
        {
            MenuList.SelectedIndex = 3;
        }

        UpdateMenuTexts();
        UpdateDynamicGreetingAndSeason();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                WindowState = WindowState.Minimized;
            }
        };

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        OpenPanelByTag("Diagnostics");
       
      
        Loaded += async (_, _) => await AnimateGreetingSpawnAsync();
    }
    private void TopBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
     
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }
    private void UpdateDynamicGreetingAndSeason()
    {
        var now = DateTime.Now;
        var name = string.IsNullOrWhiteSpace(SimpleSettings.UserName) ? "Друг" : SimpleSettings.UserName;

        // 1. Динамическое время суток
        string timeGreeting = now.Hour switch
        {
            >= 5 and < 12 => $"Доброе утро, {name}! 🌅",
            >= 12 and < 18 => $"Добрый день, {name}! ☀️",
            >= 18 and < 23 => $"Добрый вечер, {name}! 🌆",
            _ => $"Доброй ночи, {name}! 🌙"
        };

        if (GreetingText != null)
            GreetingText.Text = timeGreeting;

        // 2. Сезонные ночные / дневные подписи
        string seasonText = GetSeasonAtmosphereText(now);
        if (SeasonSubText != null)
            SeasonSubText.Text = seasonText;
    }

    private static string GetSeasonAtmosphereText(DateTime date)
    {
        bool isNight = date.Hour >= 22 || date.Hour < 6;

        return date.Month switch
        {
            12 or 1 or 2 => isNight ? "❄️ Ночной снегопад за окном..." : "❄️ Зимнее спокойствие",
            3 or 4 or 5 => isNight ? "🌱 Ночной весенний бриз..." : "🌸 Весеннее обновление",
            6 or 7 or 8 => isNight ? "🌌 Летнее звездное небо..." : "☀️ Яркий летний день",
            9 or 10 or 11 => isNight ? "🍂 Осенний ночной туман..." : "🍁 Золотая осень",
            _ => "✨ Добро пожаловать"
        };
    }

    private async Task AnimateGreetingSpawnAsync()
    {
        if (GreetingText == null || SeasonSubText == null) return;

        
        var greetingTransform = new TranslateTransform(0, 15);
        var seasonTransform = new TranslateTransform(0, 15);

        GreetingText.RenderTransform = greetingTransform;
        SeasonSubText.RenderTransform = seasonTransform;

      
        for (double progress = 0; progress <= 1; progress += 0.05)
        {
            GreetingText.Opacity = progress;
            SeasonSubText.Opacity = progress;

            greetingTransform.Y = 15 * (1 - progress);
            seasonTransform.Y = 15 * (1 - progress);

            await Task.Delay(15);
        }

        GreetingText.Opacity = 1;
        SeasonSubText.Opacity = 1;
        greetingTransform.Y = 0;
        seasonTransform.Y = 0;
    }

    private string LoadTheme()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var theme = File.ReadAllText(_configFilePath).Trim();
                if (!string.IsNullOrEmpty(theme))
                    return theme;
            }
        }
        catch { }

        return "Cyberpunk";
    }

    private void SaveTheme(string themeName)
    {
        try
        {
            File.WriteAllText(_configFilePath, themeName);
        }
        catch { }
    }

    private void ApplyThemeDirectly(string themeName)
    {
        try
        {
            var cleanName = Path.GetFileNameWithoutExtension(themeName);
            ThemeService.ChangeTheme(cleanName);
            SaveTheme(cleanName);

            if (Application.Current?.Resources.TryGetValue("MainBg", out var bg) == true && bg is IBrush brush)
            {
                this.Background = brush;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ошибка применения темы: {ex.Message}");
        }
    }

    public void AnimateThemeChange(string newThemePath, Point clickPosition)
    {
        ApplyThemeDirectly(newThemePath);
    }

    private void OnLanguageChanged() => UpdateMenuTexts();

    private void UpdateMenuTexts()
    {
        if (MenuList == null) return;

        foreach (var item in MenuList.Items.OfType<ListBoxItem>())
        {
            if (item.Tag is string tag && tag != "Black hole")
                item.Content = LocalizationService.Instance[tag];
        }
    }

    private void MenuList_SC(object? sender, SelectionChangedEventArgs e)
    {
        if (MenuList?.SelectedItem is ListBoxItem { Tag: string tag })
            OpenPanelByTag(tag);
    }

    private void OpenPanelByTag(string tag)
    {
        if (ContentArea == null) return;

        ContentArea.Content = tag switch
        {
            "Black hole" => new Black_holePanel(),
            "Settings" => new SettingsPanel(),
            "Tests" => new TestsPanel(),
            "Diagnostics" => new DiagnosticPanel(),
            "Files" => new FilesPanel(),
            "Security" => new SecurityPanel(),
            "History" => new HistoryPanel(),
            "Help" => new Help_Panel(),
            "Exit" => HandleExit(),
            _ => null
        };
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    private void ExitButton_Click(object? sender, RoutedEventArgs e) => HandleExit();

    private object? HandleExit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        return null;
    }
}
