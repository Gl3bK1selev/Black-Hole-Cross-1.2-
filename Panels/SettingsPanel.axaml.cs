using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Black_Hole_Cross;
using Black_Hole_Cross.Services;
using BlackHole;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using IoPath = System.IO.Path;

namespace BlackHole
{
    public partial class SettingsPanel : UserControl
    {
        #region Private Fields
        private DispatcherTimer _seasonTimer;
        private DispatcherTimer _rotationTimer;
        private double _rotationAngle;
        private DispatcherTimer _systemMonitorTimer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private PerformanceCounter _diskCounter;
        private DateTime _systemBootTime;
     

        private DateTime _sessionStartTime;
        private RealSystemMonitor _realMonitor;
        private readonly ObservableCollection<SystemNotification> _notificationsHistory;
        private DispatcherTimer _typingTimer;
        private string[] _aiResponses;
        private int _currentTypingStep;
        private bool _isAITyping;
        private readonly Random _random = new();
        private readonly List<Line> _shootingStars = new();
        private bool _isAnimationRunning;
        private bool _isMonitoringActive;
        private bool _isProgrammaticChange = false;
        private int _currentUpdateInterval = 5;
      
   
     
 
        #endregion

        public SettingsPanel()
        {
            InitializeComponent();
            InitSettingsAnimations();
            InitProjectStatistics();
            _notificationsHistory = new ObservableCollection<SystemNotification>();

            InitializeComponents();
            LoadSettings();
            SetupEventHandlers();
            SetupTimers();
            if(AnimationsCheckBox != null)
            {
                AnimationsCheckBox.IsChecked = SimpleSettings.EnableSeasonalAnimations;
            }
            _realMonitor = new RealSystemMonitor();
            SubscribeToNotifications();

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        #region Initialization
        private void InitializeComponents()
        {
            _aiResponses = InitializeAIResponses();

          
            var notificationsList = this.FindControl<ItemsControl>("NotificationsHistoryList");
            if (notificationsList != null)
            {
                notificationsList.ItemsSource = _notificationsHistory;
            }

            LoadHistory();
            InitializePerformanceCounters();
            GetSystemBootTime();

            _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _typingTimer.Tick += TypingTimer_Tick;
        }

        private string[] InitializeAIResponses() => new[]
        {
            LocalizationService.Instance["AIResponse1"],
            LocalizationService.Instance["AIResponse2"],
            LocalizationService.Instance["AIResponse3"],
            LocalizationService.Instance["AIResponse4"],
            LocalizationService.Instance["AIResponse5"]
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int CDS_TEST = 0x02;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISP_CHANGE_RESTART = 1;

        private void LoadSettings()
        {
            SimpleSettings.LoadSettings();
            LoadCurrentSettings();
            LoadEmailSettings();
            CheckSavedBirthday();

            LoadDisplaySettings();
            LoadUpdateModeSettings();
            LoadAdminSettings();

            // Безопасный поиск контролов и выставление значений
            SetCheckBoxState("AutoStartCheckBox", SimpleSettings.AutoStart);
            SetCheckBoxState("MinimizeToTrayCheckBox", SimpleSettings.MinimizeToTray);
            SetCheckBoxState("NotificationsCheckBox", SimpleSettings.EnableNotifications);
            SetCheckBoxState("AnimationsCheckBox", SimpleSettings.EnableSeasonalAnimations);
            SetCheckBoxState("StarsCheckBox", SimpleSettings.EnableStarAnimations);
        }

        private void SetCheckBoxState(string name, bool isChecked)
        {
            var cb = this.FindControl<CheckBox>(name);
            if (cb != null) cb.IsChecked = isChecked;
        }

        private void SetupEventHandlers()
        {
            var sendBtn = this.FindControl<Button>("SendChatButton");
            if (sendBtn != null) sendBtn.Click += SendChatButton_Click;

         

            SubscribeCheckBox("NotificationsCheckBox", NotificationsCheckBox_Changed);
            SubscribeCheckBox("AnimationsCheckBox", AnimationsCheckBox_Changed);
            SubscribeCheckBox("StarsCheckBox", StarsCheckBox_Changed);

            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            LocalizationService.Instance.SetLanguage(SimpleSettings.Language);

            UpdateLanguageButtons(SimpleSettings.Language == "ru");
        }


        private void SubscribeCheckBox(string name, EventHandler<RoutedEventArgs> handler)
        {
            var cb = this.FindControl<CheckBox>(name);
            if (cb != null)
            {
                cb.IsCheckedChanged += (s, e) => handler(s, new RoutedEventArgs());
            }
        }

        private void SetupTimers()
        {
            _rotationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _rotationTimer.Tick += RotationTimer_Tick;

            _seasonTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _seasonTimer.Tick += (s, e) => UpdateSeasonalGradient();

            _systemMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _systemMonitorTimer.Tick += UpdateSystemInfo;
        }

        private void SubscribeToNotifications()
        {
            if (_realMonitor == null) return;

            _realMonitor.OnNotification += notification =>
            {
                AddNotificationToHistory(notification);
                if (SimpleSettings.EnableNotifications)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        new NotificationWindow().ShowNotification(notification);
                    });
                }
            };
        }
        #endregion

        #region Lifecycle
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _sessionStartTime = DateTime.Now;
            CheckSavedBirthday();

            _systemMonitorTimer?.Start();
            _rotationTimer?.Start();
            _seasonTimer?.Start();
            
            UpdateSeasonalGradient();

            if (!_isAnimationRunning && SimpleSettings.EnableStarAnimations)
            {
                _isAnimationRunning = true;
                CreateStars();
                CreateParticles();
                StartShootingStarGenerator();
            }

            StartMonitoringAsync();
            ApplyWindowMode(SimpleSettings.WindowMode);
        }

        private async void StartMonitoringAsync()
        {
            await Task.Delay(3000);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ShowWelcomeNotification();
                if (SimpleSettings.EnableNotifications)
                {
                    StartRealMonitoring();
                }
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _realMonitor?.StopMonitoring();
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

            _systemMonitorTimer?.Stop();
            _typingTimer?.Stop();
            _rotationTimer?.Stop();
            _seasonTimer?.Stop();
        }
        #endregion

        #region Language Methods
        private void RussianBtn_Click(object? sender, RoutedEventArgs e)
        {
            LocalizationService.Instance.SetLanguage("ru");
            UpdateLanguageButtons(true);
            ForceRefreshBindings();
        }

        private void EnglishBtn_Click(object? sender, RoutedEventArgs e)
        {
            LocalizationService.Instance.SetLanguage("en");
            UpdateLanguageButtons(false);
            ForceRefreshBindings();
        }

        private void UpdateLanguageButtons(bool isRussian)
        {
            var ruBtn = this.FindControl<Button>("RussianBtn");
            var enBtn = this.FindControl<Button>("EnglishBtn");

            if (ruBtn != null)
            {
                ruBtn.Background = GetLanguageButtonBrush(isRussian);
                ruBtn.Foreground = isRussian ? Brushes.White : new SolidColorBrush(Color.Parse("#888888"));
            }

            if (enBtn != null)
            {
                enBtn.Background = GetLanguageButtonBrush(!isRussian);
                enBtn.Foreground = !isRussian ? Brushes.White : new SolidColorBrush(Color.Parse("#888888"));
            }
        }

        private SolidColorBrush GetLanguageButtonBrush(bool isActive) =>
            isActive
                ? new SolidColorBrush(Color.Parse("#4050B0"))
                : new SolidColorBrush(Color.Parse("#2A2A2A"));

        private void ForceRefreshBindings()
        {
            var context = DataContext;
            DataContext = null;
            DataContext = context;
        }
        #endregion

        #region Display Settings
        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }

        private ComboBox? GetComboBox(string name) => this.FindControl<ComboBox>(name);

        private void ApplyResolution(int width, int height)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null && mainWindow.WindowState == WindowState.Normal)
            {
                mainWindow.Width = width;
                mainWindow.Height = height;
            }
        }

        private void ApplyScaling(int scalingPercent)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                double scale = scalingPercent / 100.0;

                if (mainWindow.Content is Control rootElement)
                {
                    if (Math.Abs(scale - 1.0) < 0.01)
                    {
                       
                        rootElement.RenderTransform = null;
                    }
                    else
                    {
                       
                        rootElement.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
                        rootElement.RenderTransform = new ScaleTransform(scale, scale);
                    }
                }

                SimpleSettings.SaveDisplayScaling(scalingPercent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка применения масштабирования: {ex.Message}");
            }
        }

        private void ApplyWindowMode(string mode)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                switch (mode)
                {
                    case "Полноэкранный режим":
                        mainWindow.WindowState = WindowState.FullScreen;
                        break;
                    case "Безрамочный оконный":
                        mainWindow.WindowState = WindowState.Maximized;
                        break;
                    case "Оконный режим":
                    default:
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.CanResize = true;
                        break;
                }

                ShowSystemNotification("🪟 Режим окна изменен", $"Режим: {mode}", "#87CEEB");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка применения режима окна: {ex.Message}");
            }
        }

        private void ApplyAllDisplaySettings()
        {
            try
            {
                var resSelector = GetComboBox("ResolutionSelector");
                if (resSelector?.SelectedItem is ComboBoxItem resItem)
                {
                    string selectedRes = resItem.Content?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(selectedRes) && selectedRes.Contains("x"))
                    {
                        var parts = selectedRes.Split('x');
                        if (int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
                            ApplyResolution(width, height);
                    }
                }

                var scalingSelector = GetComboBox("ScalingSelector");
                if (scalingSelector?.SelectedItem is ComboBoxItem scaleItem)
                {
                    string cleanValue = scaleItem.Content?.ToString()?.Replace("%", "").Trim() ?? "";
                    if (int.TryParse(cleanValue, out int scalingPercent))
                        ApplyScaling(scalingPercent);
                }

                var windowModeSelector = GetComboBox("WindowModeSelector");
                if (windowModeSelector?.SelectedItem is ComboBoxItem modeItem)
                    ApplyWindowMode(modeItem.Content?.ToString() ?? "");

                ShowSystemNotification("⚙️ Настройки применены", "Параметры успешно обновлены", "#9370DB");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка применения настроек: {ex.Message}");
            }
        }

        private void ApplyDisplaySettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            var resSelector = GetComboBox("ResolutionSelector");
            if (resSelector?.SelectedItem is ComboBoxItem resItem)
            {
                string resolution = resItem.Content?.ToString() ?? "";
                if (resolution != "Загрузка разрешений...")
                    SimpleSettings.SaveDisplayResolution(resolution);
            }

            var scalingSelector = GetComboBox("ScalingSelector");
            if (scalingSelector?.SelectedItem is ComboBoxItem scaleItem)
            {
                string scaling = scaleItem.Content?.ToString()?.Replace("%", "") ?? "";
                if (int.TryParse(scaling, out int scalingValue))
                    SimpleSettings.SaveDisplayScaling(scalingValue);
            }

            var windowModeSelector = GetComboBox("WindowModeSelector");
            if (windowModeSelector?.SelectedItem is ComboBoxItem modeItem)
                SimpleSettings.SaveWindowMode(modeItem.Content?.ToString() ?? "");

            var monitorSelector = GetComboBox("MonitorSelector");
            if (monitorSelector?.SelectedItem is ComboBoxItem monitorItem)
                SimpleSettings.SaveDisplayMonitor(monitorItem.Content?.ToString() ?? "");

            ApplyAllDisplaySettings();
        }

        private void MonitorSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var monitorSelector = GetComboBox("MonitorSelector");
            if (_isProgrammaticChange || monitorSelector?.SelectedItem == null) return;
            if (monitorSelector.SelectedItem is ComboBoxItem item)
            {
                SimpleSettings.SaveDisplayMonitor(item.Content?.ToString() ?? "");
                RefreshResolutions();
                ShowSettingNotification($"Монитор: {item.Content}", true);
            }
        }

        private void ResolutionSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var resSelector = GetComboBox("ResolutionSelector");
            if (_isProgrammaticChange || resSelector?.SelectedItem == null) return;
            if (resSelector.SelectedItem is ComboBoxItem item)
            {
                string res = item.Content?.ToString() ?? "";
                if (res != "Загрузка разрешений...")
                {
                    SimpleSettings.SaveDisplayResolution(res);
                    ShowSettingNotification($"Разрешение: {res}", true);
                }
            }
        }

        private void ScalingSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var scalingSelector = GetComboBox("ScalingSelector");
            if (_isProgrammaticChange || scalingSelector?.SelectedItem == null) return;
            if (scalingSelector.SelectedItem is ComboBoxItem item)
            {
                string cleanValue = item.Content?.ToString()?.Replace("%", "").Trim() ?? "";
                if (int.TryParse(cleanValue, out int percent))
                {
                    SimpleSettings.SaveDisplayScaling(percent);
                    ApplyScaling(percent);
                    ShowSettingNotification($"Масштаб: {percent}%", true);
                }
            }
        }

        private void WindowModeSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isProgrammaticChange || !IsLoaded) return;
            var windowModeSelector = GetComboBox("WindowModeSelector");
            if (windowModeSelector?.SelectedItem is ComboBoxItem item)
            {
                string selectedMode = item.Content?.ToString() ?? "";
                if (!string.IsNullOrEmpty(selectedMode))
                    ApplyWindowMode(selectedMode);
            }
        }

        private void LoadDisplaySettings()
        {
            var monitorSelector = GetComboBox("MonitorSelector");
            var resSelector = GetComboBox("ResolutionSelector");
            var scalingSelector = GetComboBox("ScalingSelector");
            var windowModeSelector = GetComboBox("WindowModeSelector");

            if (monitorSelector == null || resSelector == null ||
                scalingSelector == null || windowModeSelector == null)
            {
                Debug.WriteLine("❌ Один из ComboBox-ов не инициализирован!");
                return;
            }

            _isProgrammaticChange = true;
            try
            {
                var monitors = GetMonitorsList();

                var monitorItems = new List<ComboBoxItem>();
                if (monitors.Count == 0)
                    monitorItems.Add(new ComboBoxItem { Content = "Основной монитор" });
                else
                {
                    foreach (var monitor in monitors)
                        monitorItems.Add(new ComboBoxItem { Content = monitor });
                }
                monitorSelector.ItemsSource = monitorItems;

                var savedMonitor = SimpleSettings.DisplayMonitor;
                if (!string.IsNullOrEmpty(savedMonitor))
                {
                    foreach (ComboBoxItem item in monitorItems)
                    {
                        if (item.Content?.ToString() == savedMonitor)
                        {
                            monitorSelector.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (monitorSelector.SelectedItem == null && monitorItems.Count > 0)
                    monitorSelector.SelectedIndex = 0;

                RefreshResolutions();

                var scaleItems = new List<ComboBoxItem>();
                string[] scales = { "100%", "125%", "150%", "175%", "200%" };
                foreach (var scale in scales)
                    scaleItems.Add(new ComboBoxItem { Content = scale });

                scalingSelector.ItemsSource = scaleItems;

                int savedScaling = SimpleSettings.DisplayScaling;
                if (savedScaling <= 0) savedScaling = 100;

                string targetScale = $"{savedScaling}%";
                foreach (ComboBoxItem item in scaleItems)
                {
                    if (item.Content?.ToString() == targetScale)
                    {
                        scalingSelector.SelectedItem = item;
                        break;
                    }
                }

                if (scalingSelector.SelectedItem == null && scaleItems.Count > 0)
                    scalingSelector.SelectedIndex = 0;

              
                if (savedScaling != 100)
                    ApplyScaling(savedScaling);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка загрузки настроек экрана: {ex.Message}");
            }
            finally
            {
                _isProgrammaticChange = false;
            }
        }

        private void RefreshResolutions()
        {
            var resSelector = GetComboBox("ResolutionSelector");
            if (resSelector == null) return;

            try
            {
                var resolutions = GetAvailableResolutions();
                var resItems = new List<ComboBoxItem>();

                if (resolutions.Count == 0)
                {
                    resItems.Add(new ComboBoxItem { Content = "1920x1080" });
                    resItems.Add(new ComboBoxItem { Content = "1366x768" });
                    resItems.Add(new ComboBoxItem { Content = "1280x720" });
                }
                else
                {
                    foreach (var res in resolutions)
                        resItems.Add(new ComboBoxItem { Content = res });
                }

                resSelector.ItemsSource = resItems;

                var savedRes = SimpleSettings.DisplayResolution;
                bool found = false;
                foreach (ComboBoxItem item in resItems)
                {
                    if (item.Content?.ToString() == savedRes)
                    {
                        resSelector.SelectedItem = item;
                        found = true;
                        break;
                    }
                }

                if (!found && resItems.Count > 0)
                    resSelector.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка обновления разрешений: {ex.Message}");
                var fallbackItems = new List<ComboBoxItem>
        {
            new ComboBoxItem { Content = "1920x1080" },
            new ComboBoxItem { Content = "1366x768" },
            new ComboBoxItem { Content = "1280x720" }
        };
                resSelector.ItemsSource = fallbackItems;
                resSelector.SelectedIndex = 0;
            }
        }

        private List<string> GetMonitorsList()
        {
            var monitors = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var displayDevice = new DISPLAY_DEVICE();
                    displayDevice.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                    for (uint i = 0; EnumDisplayDevices(null!, i, ref displayDevice, 0); i++)
                    {
                        if ((displayDevice.StateFlags & 1) != 0)
                            monitors.Add(displayDevice.DeviceString);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Ошибка получения списка мониторов: {ex.Message}");
                }
            }

            if (monitors.Count == 0)
                monitors.Add("Основной монитор");

            return monitors;
        }

        private List<string> GetAvailableResolutions()
        {
            var resolutions = new HashSet<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                    for (int i = 0; EnumDisplaySettings(null!, i, ref devMode); i++)
                    {
                        if (devMode.dmPelsWidth >= 1024 && devMode.dmPelsHeight >= 720)
                            resolutions.Add($"{devMode.dmPelsWidth}x{devMode.dmPelsHeight}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Ошибка получения разрешений: {ex.Message}");
                }
            }

            return resolutions.OrderByDescending(s => int.TryParse(s.Split('x')[0], out int w) ? w : 0).ToList();
        }
        #endregion



        #region Theme Methods
        private void SetDarkTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("DarkTheme");
        private void SetLightTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("LightTheme");
     
        private void SetCyberPunkTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("Cyberpunk");
        private void SetAMOLEDTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("AMOLED");
        private void SetTokyoTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("TokyoNight");
        private void SetMatrixTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("Matrix");
        private void SetRedTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("SithRed");
        private void SetNordicTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("NordicForest");
        private void SetNebulaTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("Nebula");
        private void SetMetallTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("IndustrialMetallic");
        private void SetSynthTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("Synthwave80s");
        private void SetCosmosTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("DeepCosmos");
        private void SetMoonTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("BloodMoon");
        private void SetOceanTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("OceanAbyss");
        private void SetGoldTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("SolarGold");
        private void SetGlassTheme_Click(object? sender, RoutedEventArgs e) => ApplyTheme("FrostGlass");

        private void ApplyTheme(string themeName)
        {
            try
            {
                // Переключаем тему через исправленный сервисный класс
                ThemeService.ChangeTheme(themeName);

                // Сохраняем настройки
                SimpleSettings.SaveTheme(themeName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка смены темы в SettingsPanel: {ex.Message}");
            }
        }
       

        
        #endregion



        #region Update Mode
        private void UpdateMode_Click(object? sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            if (int.TryParse(button.Tag?.ToString(), out int interval))
            {
                _currentUpdateInterval = interval;
                UpdateModeButtonsVisual(button);
                SystemMonitorService.Instance.SetInterval(interval);
                SimpleSettings.SaveUpdateInterval(interval);
                UpdateCurrentModeText(interval);

                string modeName = interval switch { 2 => "Real Time", 5 => "Обычный", 15 => "Энергосбережение", _ => "Обычный" };
                ShowSystemNotification($"🔄 Режим обновления", $"Изменен на: {modeName} ({interval} сек)",
                    interval == 2 ? "#9370DB" : interval == 5 ? "#4050B0" : "#40B050");
            }
        }

        private void UpdateModeButtonsVisual(Button activeButton)
        {
            var realTimeBtn = this.FindControl<Button>("RealTimeModeBtn");
            var normalBtn = this.FindControl<Button>("NormalModeBtn");
            var ecoBtn = this.FindControl<Button>("EcoModeBtn");

            var buttons = new[] { realTimeBtn, normalBtn, ecoBtn };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                if (btn == activeButton)
                {
                    btn.Background = btn.Tag?.ToString() == "2"
                        ? new SolidColorBrush(Color.Parse("#9370DB"))
                        : btn.Tag?.ToString() == "5"
                        ? new SolidColorBrush(Color.Parse("#4050B0"))
                        : new SolidColorBrush(Color.Parse("#40B050"));
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.Parse("#2A2A2A"));
                    btn.Foreground = new SolidColorBrush(Color.Parse("#888888"));
                }
            }
        }

        private void UpdateCurrentModeText(int interval)
        {
            string modeText = interval switch
            {
                2 => "🚀 Real Time режим (обновление каждые 2 секунды)",
                5 => "⚡ Обычный режим (обновление каждые 5 секунд)",
                15 => "🌱 Энергосбережение (обновление каждые 15 секунд)",
                _ => "⚡ Обычный режим"
            };

            var currentModeText = this.FindControl<TextBlock>("CurrentUpdateModeText");
            if (currentModeText != null)
            {
                currentModeText.Text = modeText;
                currentModeText.Foreground = interval switch
                {
                    2 => new SolidColorBrush(Color.Parse("#9370DB")),
                    5 => new SolidColorBrush(Color.Parse("#4050B0")),
                    15 => new SolidColorBrush(Color.Parse("#40B050")),
                    _ => new SolidColorBrush(Color.Parse("#4050B0"))
                };
            }
        }

        private void LoadUpdateModeSettings()
        {
            var realTimeBtn = this.FindControl<Button>("RealTimeModeBtn");
            var normalBtn = this.FindControl<Button>("NormalModeBtn");
            var ecoBtn = this.FindControl<Button>("EcoModeBtn");

            if (realTimeBtn == null || normalBtn == null || ecoBtn == null)
            {
                Debug.WriteLine("❌ Кнопки режимов не инициализированы!");
                return;
            }

            var savedInterval = SimpleSettings.UpdateInterval;
            if (savedInterval <= 0) savedInterval = 5;
            _currentUpdateInterval = savedInterval;

            Button? activeButton = savedInterval switch { 2 => realTimeBtn, 5 => normalBtn, 15 => ecoBtn, _ => normalBtn };
            if (activeButton != null)
            {
                UpdateModeButtonsVisual(activeButton);
                UpdateCurrentModeText(savedInterval);
                SystemMonitorService.Instance.SetInterval(savedInterval);
            }
        }
        #endregion

        #region Admin Rights
        private void RunAsAdminToggle_Checked(object? sender, RoutedEventArgs e)
        {
            if (_isProgrammaticChange) return;
            SimpleSettings.SaveRunAsAdmin(true);
            RestartAsAdmin();
        }

        private void RunAsAdminToggle_Unchecked(object? sender, RoutedEventArgs e)
        {
            if (_isProgrammaticChange) return;
            SimpleSettings.SaveRunAsAdmin(false);
            ShowSettingNotification("Запуск от имени администратора", false);
        }

        private void RestartAsAdmin()
        {
            try
            {
                var appPath = GetAppPath();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var process = new ProcessStartInfo
                    {
                        FileName = appPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(process);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                  
                    var process = new ProcessStartInfo
                    {
                        FileName = "pkexec",
                        Arguments = $"\"{appPath}\"",
                        UseShellExecute = true
                    };
                    Process.Start(process);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                 
                    var process = new ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e 'do shell script \"\\\"{appPath}\\\"\" with administrator privileges'",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(process);
                }

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
            
                RevertAdminToggle(false);
                ShowSystemNotification("❌ Ошибка", $"Не удалось перезапустить с правами администратора: {ex.Message}", "#FF4444");
            }
        }

        private void LoadAdminSettings()
        {
            var control = this.FindControl<Control>("RunAsAdminToggle");
            if (control == null) return;

            _isProgrammaticChange = true;

            if (control is CheckBox checkBox)
                checkBox.IsChecked = SimpleSettings.RunAsAdmin;
            else if (control is ToggleButton toggleButton)
                toggleButton.IsChecked = SimpleSettings.RunAsAdmin;
            else if (control is ToggleSwitch toggleSwitch)
                toggleSwitch.IsChecked = SimpleSettings.RunAsAdmin;

            _isProgrammaticChange = false;
        }

        private void RevertAdminToggle(bool value)
        {
            SimpleSettings.SaveRunAsAdmin(value);
            var control = this.FindControl<Control>("RunAsAdminToggle");
            if (control == null) return;

            _isProgrammaticChange = true;

            if (control is CheckBox checkBox)
                checkBox.IsChecked = value;
            else if (control is ToggleButton toggleButton)
                toggleButton.IsChecked = value;
            else if (control is ToggleSwitch toggleSwitch)
                toggleSwitch.IsChecked = value;

            _isProgrammaticChange = false;
        }
        #endregion

        #region Additional Settings
        private void SettingCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null || _isProgrammaticChange) return;

            bool isEnabled = checkBox.IsChecked == true;
            string settingName = "";

            switch (checkBox.Name)
            {
                case "AutoStartCheckBox":
                    SimpleSettings.AutoStart = isEnabled;
                    settingName = "Автозагрузка";
                    SetAutoStart(isEnabled);
                    break;
                case "MinimizeToTrayCheckBox":
                    SimpleSettings.MinimizeToTray = isEnabled;
                    settingName = "Сворачивание в трей";
                    break;
                case "AnimationsCheckBox":
                    SimpleSettings.EnableSeasonalAnimations = isEnabled;
                    settingName = "Анимации";
                    break;
                case "StarsCheckBox":
                    SimpleSettings.EnableStarAnimations = isEnabled;
                    settingName = "Звезды";
                    break;
            }

            if (!string.IsNullOrEmpty(settingName))
            {
                SimpleSettings.SaveSettings();
                ShowSettingNotification(settingName, isEnabled);
            }
        }
        #endregion

        #region Email Methods
        private void EmailTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var emailTextBox = this.FindControl<TextBox>("EmailTextBox");
            if (emailTextBox != null && !string.IsNullOrEmpty(emailTextBox.Text) && emailTextBox.Text.Contains("@"))
            {
                SimpleSettings.SaveEmail(emailTextBox.Text.Trim());
                ShowEmailStatus("✅ Почта сохранена");
            }
        }

        private void BindEmailButton_Click(object? sender, RoutedEventArgs e)
        {
            var emailTextBox = this.FindControl<TextBox>("EmailTextBox");
            string email = emailTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(email) || !email.Contains("@") || !email.Contains("."))
            {
                ShowEmailStatus("❌ Введите корректный email");
                return;
            }

            SimpleSettings.SaveEmail(email);
            ShowEmailStatus("✅ Почта привязана!");
            ShowSystemNotification("📧 Почта привязана", "Теперь вы сможете получать уведомления", "#00FF88");
        }

        private void ShowEmailStatus(string message)
        {
            var emailTextBox = this.FindControl<TextBox>("EmailTextBox");
            if (emailTextBox == null) return;

            ToolTip.SetTip(emailTextBox, message);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                ToolTip.SetTip(emailTextBox, "Для получения уведомлений и ответов на отзывы");
            };
            timer.Start();
        }

        private void LoadEmailSettings()
        {
            var emailTextBox = this.FindControl<TextBox>("EmailTextBox");
            if (emailTextBox != null && !string.IsNullOrEmpty(SimpleSettings.UserEmail))
                emailTextBox.Text = SimpleSettings.UserEmail;
        }
        #endregion

        #region Game Optimization
        private bool _isGameOptimized = false;

        private void ToggleGameOptimization_Click(object? sender, RoutedEventArgs e)
        {
            if (_isGameOptimized) _ = DisableGameOptimization();
            else _ = EnableGameOptimization();
        }

        private void DisableGameOptimization_Click(object? sender, RoutedEventArgs e) => _ = DisableGameOptimization();

        private async Task EnableGameOptimization()
        {
            var optimizeGameBtn = this.FindControl<Button>("OptimizeGameBtn");
            if (optimizeGameBtn == null) return;

            try
            {
                optimizeGameBtn.IsEnabled = false;
                optimizeGameBtn.Content = "⏳ ВКЛЮЧЕНИЕ...";

                await Task.Run(() =>
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        SetGameModeRegistry(true);
                        SetPerformanceOptions(true);
                        EnableAdditionalOptimizations();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        EnableLinuxGameMode();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        EnableMacOsGameMode();
                    }
                });

                _isGameOptimized = true;
                UpdateOptimizationButtons();
                ShowSystemNotification("🎮 Оптимизация ВКЛЮЧЕНА", "Игровой режим активирован", "#00FF88");
            }
            catch (Exception ex)
            {
                ShowSystemNotification("❌ Ошибка включения", ex.Message, "#FF4444");
            }
            finally
            {
                optimizeGameBtn.Content = "🎮 ВКЛ. ОПТИМИЗАЦИЮ";
                optimizeGameBtn.IsEnabled = true;
            }
        }

        private async Task DisableGameOptimization()
        {
            var disableOptimizeBtn = this.FindControl<Button>("DisableOptimizeBtn");
            if (disableOptimizeBtn == null) return;

            try
            {
                disableOptimizeBtn.IsEnabled = false;
                disableOptimizeBtn.Content = "⏳ ВЫКЛЮЧЕНИЕ...";

                await Task.Run(() =>
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        SetGameModeRegistry(false);
                        SetPerformanceOptions(false);
                        DisableAdditionalOptimizations();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        DisableLinuxGameMode();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        DisableMacOsGameMode();
                    }
                });

                _isGameOptimized = false;
                UpdateOptimizationButtons();
                ShowSystemNotification("🔴 Оптимизация ВЫКЛЮЧЕНА", "Игровой режим деактивирован", "#FFA500");
            }
            catch (Exception ex)
            {
                ShowSystemNotification("❌ Ошибка выключения", ex.Message, "#FF4444");
            }
            finally
            {
                disableOptimizeBtn.Content = "🔴 ВЫКЛ. ОПТИМИЗАЦИЮ";
                disableOptimizeBtn.IsEnabled = true;
            }
        }

        private void UpdateOptimizationButtons()
        {
            var optimizeGameBtn = this.FindControl<Button>("OptimizeGameBtn");
            var disableOptimizeBtn = this.FindControl<Button>("DisableOptimizeBtn");

            if (optimizeGameBtn == null || disableOptimizeBtn == null) return;

            if (_isGameOptimized)
            {
                optimizeGameBtn.IsVisible = false;
                disableOptimizeBtn.IsVisible = true;
            }
            else
            {
                optimizeGameBtn.IsVisible = true;
                disableOptimizeBtn.IsVisible = false;
            }
        }

        #region Windows Optimization
        private void SetGameModeRegistry(bool enable)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\GameBar", true);
                key?.SetValue("AllowAutoGameMode", enable ? 1 : 0, RegistryValueKind.DWord);
                key?.SetValue("AutoGameModeEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка Game Mode: {ex.Message}"); }
        }

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr rootKey, ref Guid schemeGuid);

        private void SetPerformanceOptions(bool enable)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                var schemeGuid = enable
                    ? new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") // Высокая производительность
                    : new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"); // Сбалансированная
                PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка схемы питания: {ex.Message}"); }
        }

        private void EnableAdditionalOptimizations() => SetBackgroundAppsState(0);
        private void DisableAdditionalOptimizations() => SetBackgroundAppsState(1);

        private void SetBackgroundAppsState(int value)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", true);
                if (key?.GetValueNames() is { } names)
                {
                    foreach (var app in names)
                        try { key.SetValue(app, value, RegistryValueKind.DWord); } catch { }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка фоновых приложений: {ex.Message}"); }
        }
        #endregion

        #region Linux Optimization
        private void EnableLinuxGameMode()
        {
            try
            {
               
                RunShellCommand("powerprofilesctl", "set performance");

              
                RunShellCommand("gamemoded", "-r");
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка Linux GameMode: {ex.Message}"); }
        }

        private void DisableLinuxGameMode()
        {
            try
            {
             
                RunShellCommand("powerprofilesctl", "set balanced");
                RunShellCommand("gamemoded", "-s");
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка Linux GameMode: {ex.Message}"); }
        }
        #endregion

        #region macOS Optimization
        private void EnableMacOsGameMode()
        {
            try
            {
             
                RunShellCommand("purge", "");
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка macOS Optimization: {ex.Message}"); }
        }

        private void DisableMacOsGameMode()
        {
           
        }
        #endregion

        private void RunShellCommand(string cmd, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                process?.WaitForExit(2000); 
            }
            catch { }
        }
        #endregion


        #region AutoStart
        private bool SetAutoStart(bool enable)
        {
            try
            {
                var appPath = GetAppPath();
                if (!File.Exists(appPath))
                {
                    ShowErrorNotification($"Файл приложения не найден: {appPath}");
                    return false;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return SetAutoStartWindows(enable, appPath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return SetAutoStartLinux(enable, appPath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return SetAutoStartMacOs(enable, appPath);
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка изменения автозагрузки: {ex.Message}");
                ShowErrorNotification($"Не удалось изменить автозагрузку: {ex.Message}");
                return false;
            }
        }

        private void CheckAutoStartStatus()
        {
            try
            {
                bool isEnabled = false;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    isEnabled = CheckAutoStartWindows();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    isEnabled = CheckAutoStartLinux();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    isEnabled = CheckAutoStartMacOs();
                }

                var autoStartCheckBox = this.FindControl<CheckBox>("AutoStartCheckBox");

                _isProgrammaticChange = true;
                if (autoStartCheckBox != null) autoStartCheckBox.IsChecked = isEnabled;
                SimpleSettings.AutoStart = isEnabled;
                _isProgrammaticChange = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка проверки автозагрузки: {ex.Message}");
            }
        }

        private string GetAppPath() => Process.GetCurrentProcess().MainModule?.FileName
            ?? Assembly.GetEntryAssembly()?.Location
            ?? Assembly.GetExecutingAssembly().Location;

        private void RevertAutoStartCheckbox(bool value)
        {
            var autoStartCheckBox = this.FindControl<CheckBox>("AutoStartCheckBox");
            if (autoStartCheckBox == null) return;

            _isProgrammaticChange = true;
            autoStartCheckBox.IsChecked = value;
            _isProgrammaticChange = false;
        }

        #region Windows AutoStart
        private bool SetAutoStartWindows(bool enable, string appPath)
        {
            const string appName = "BlackHole";
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (enable) key?.SetValue(appName, $"\"{appPath}\"");
            else key?.DeleteValue(appName, false);

            return true;
        }

        private bool CheckAutoStartWindows()
        {
            const string appName = "BlackHole";
            var currentPath = GetAppPath();

            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            var registryValue = key?.GetValue(appName) as string;
            var registryPath = registryValue?.Trim('"').Trim();

            return !string.IsNullOrEmpty(registryPath) &&
                   string.Equals(IoPath.GetFullPath(registryPath), IoPath.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region Linux AutoStart
        private string GetLinuxAutostartFilePath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return IoPath.Combine(home, ".config", "autostart", "blackhole.desktop");
        }

        private bool SetAutoStartLinux(bool enable, string appPath)
        {
            string filePath = GetLinuxAutostartFilePath();

            if (enable)
            {
                string dir = IoPath.GetDirectoryName(filePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string content = $"[Desktop Entry]\n" +
                                 $"Type=Application\n" +
                                 $"Name=BlackHole\n" +
                                 $"Exec=\"{appPath}\"\n" +
                                 $"Terminal=false\n" +
                                 $"X-GNOME-Autostart-enabled=true\n";

                File.WriteAllText(filePath, content);
            }
            else
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }

            return true;
        }

        private bool CheckAutoStartLinux()
        {
            string filePath = GetLinuxAutostartFilePath();
            return File.Exists(filePath);
        }
        #endregion

        #region macOS AutoStart
        private string GetMacOsPlistPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return IoPath.Combine(home, "Library", "LaunchAgents", "com.blackhole.app.plist");
        }

        private bool SetAutoStartMacOs(bool enable, string appPath)
        {
            string filePath = GetMacOsPlistPath();

            if (enable)
            {
                string dir = IoPath.GetDirectoryName(filePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string content = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.blackhole.app</string>
    <key>ProgramArguments</key>
    <array>
        <string>{appPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";

                File.WriteAllText(filePath, content);
            }
            else
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }

            return true;
        }

        private bool CheckAutoStartMacOs()
        {
            string filePath = GetMacOsPlistPath();
            return File.Exists(filePath);
        }
        #endregion
        #endregion


        #region Notification Methods
        private void NotificationsCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            var notificationsCheckBox = this.FindControl<CheckBox>("NotificationsCheckBox");
            bool isEnabled = notificationsCheckBox?.IsChecked == true;
            SimpleSettings.SaveNotifications(isEnabled);

            if (isEnabled && !_isMonitoringActive) StartRealMonitoring();
            else if (!isEnabled && _isMonitoringActive)
            {
                _realMonitor.StopMonitoring();
                _isMonitoringActive = false;
            }
        }

        private void StartRealMonitoring()
        {
            if (!_isMonitoringActive && SimpleSettings.EnableNotifications)
            {
                _realMonitor.StartMonitoring();
                _isMonitoringActive = true;
            }
        }

        public void AddNotificationToHistory(SystemNotification notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                notification.Timestamp = DateTime.Now;
                _notificationsHistory.Insert(0, notification);
                if (_notificationsHistory.Count > 50)
                    _notificationsHistory.RemoveAt(_notificationsHistory.Count - 1);
                UpdateHistoryStats();
            });
        }

        private void ShowSystemNotification(string title, string message, string color)
        {
            var notification = new SystemNotification
            {
                Title = title,
                Message = message,
                Icon = GetIconFromTitle(title),
                Type = GetNotificationTypeFromTitle(title)
            };
            AddNotificationToHistory(notification);
            if (SimpleSettings.EnableNotifications)
                new NotificationWindow().ShowNotification(notification);
        }

        private void ShowErrorNotification(string message) =>
            AddNotificationToHistory(new SystemNotification { Title = "❌ Ошибка", Message = message, Icon = "⚠️", Type = NotificationType.Error });

        private void ShowWelcomeNotification() =>
            ShowSystemNotification("Система запущена", "Все компоненты работают нормально", "#00FF88");

        private string GetIconFromTitle(string title)
        {
            if (title.Contains("✅") || title.Contains("✔")) return "✅";
            if (title.Contains("❌")) return "❌";
            if (title.Contains("🔴")) return "🔴";
            if (title.Contains("🎮")) return "🎮";
            return "ℹ️";
        }

        private NotificationType GetNotificationTypeFromTitle(string title)
        {
            if (title.Contains("Ошибка") || title.Contains("❌")) return NotificationType.Error;
            if (title.Contains("ВНИМАНИЕ") || title.Contains("🔴")) return NotificationType.Warning;
            if (title.Contains("✅") || title.Contains("✔")) return NotificationType.Success;
            return NotificationType.Info;
        }

        private void ShowSettingNotification(string settingName, bool isEnabled) =>
            ShowSystemNotification(isEnabled ? "⚡ Настройка включена" : "💤 Настройка отключена",
                $"{settingName} {(isEnabled ? "активирована" : "деактивирована")}",
                isEnabled ? "#00FF88" : "#FFA500");

        private void FilterNotifications_Click(object? sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var filterAllBtn = this.FindControl<Button>("FilterAllBtn");
            var filterCriticalBtn = this.FindControl<Button>("FilterCriticalBtn");
            var filterWarningBtn = this.FindControl<Button>("FilterWarningBtn");
            var filterInfoBtn = this.FindControl<Button>("FilterInfoBtn");
            var listContainer = this.FindControl<ListBox>("NotificationsHistoryList") ?? (Control?)this.FindControl<ItemsControl>("NotificationsHistoryList");

            ResetFilterButtons();
            button.Background = new SolidColorBrush(Color.Parse("#4050B0"));

            if (listContainer == null) return;

            if (button == filterAllBtn)
                SetItemsSource(listContainer, _notificationsHistory);
            else if (button == filterCriticalBtn)
                SetItemsSource(listContainer, new ObservableCollection<SystemNotification>(
                    _notificationsHistory.Where(n => n.Type == NotificationType.Critical || n.Type == NotificationType.Error)));
            else if (button == filterWarningBtn)
                SetItemsSource(listContainer, new ObservableCollection<SystemNotification>(
                    _notificationsHistory.Where(n => n.Type == NotificationType.Warning)));
            else if (button == filterInfoBtn)
                SetItemsSource(listContainer, new ObservableCollection<SystemNotification>(
                    _notificationsHistory.Where(n => n.Type == NotificationType.Info || n.Type == NotificationType.Success)));
        }

        private void SetItemsSource(Control control, object items)
        {
            if (control is ItemsControl itemsControl)
                itemsControl.ItemsSource = items as IEnumerable;
        }

        private void ResetFilterButtons()
        {
            var defaultBg = new SolidColorBrush(Color.Parse("#2A2A2A"));
            var buttons = new[] {
        this.FindControl<Button>("FilterAllBtn"),
        this.FindControl<Button>("FilterCriticalBtn"),
        this.FindControl<Button>("FilterWarningBtn"),
        this.FindControl<Button>("FilterInfoBtn")
    };

            foreach (var btn in buttons)
            {
                if (btn != null) btn.Background = defaultBg;
            }
        }

        private void ClearHistoryBtn_Click(object? sender, RoutedEventArgs e)
        {
            _notificationsHistory.Clear();
            UpdateHistoryStats();
            ShowSystemNotification("✅ История очищена", "Все уведомления удалены", "#00FF88");
        }

        private void UpdateHistoryStats()
        {
            var historyStatsText = this.FindControl<TextBlock>("HistoryStatsText");
            if (historyStatsText == null) return;

            var total = _notificationsHistory.Count;
            var today = _notificationsHistory.Count(n => n.Timestamp.Date == DateTime.Today);
            var critical = _notificationsHistory.Count(n => n.Type == NotificationType.Critical);
            historyStatsText.Text = $"Всего: {total} | Сегодня: {today} | Критич.: {critical}";
        }

        private void LoadHistory()
        {
            if (_notificationsHistory.Count == 0)
            {
                _notificationsHistory.Add(new SystemNotification
                {
                    Title = "Система запущена",
                    Message = "Все компоненты работают нормально",
                    Icon = "✅",
                    Type = NotificationType.Success,
                    Timestamp = DateTime.Now.AddMinutes(-30)
                });
            }
            UpdateHistoryStats();
        }
        #endregion

        #region System Monitoring
      
       
        private readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private readonly bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        private readonly bool _isOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private void InitializePerformanceCounters()
        {
            GetSystemBootTime();

            if (_isWindows)
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuCounter.NextValue(); // Первый вызов прогревочный
                }
                catch (Exception ex) { Debug.WriteLine($"❌ Ошибка инициализации ЦП Windows: {ex.Message}"); }
            }
        }

        private void GetSystemBootTime()
        {
            try
            {
                if (_isWindows)
                {
                    using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var bootTimeString = obj["LastBootUpTime"]?.ToString();
                        if (!string.IsNullOrEmpty(bootTimeString))
                        {
                            _systemBootTime = ManagementDateTimeConverter.ToDateTime(bootTimeString);
                            return;
                        }
                    }
                }
                else if (_isLinux && File.Exists("/proc/uptime"))
                {
                    string uptimeText = File.ReadAllText("/proc/uptime").Split(' ')[0];
                    if (double.TryParse(uptimeText, NumberStyles.Any, CultureInfo.InvariantCulture, out double uptimeSeconds))
                    {
                        _systemBootTime = DateTime.Now.AddSeconds(-uptimeSeconds);
                        return;
                    }
                }
                else if (_isOSX)
                {
                    // Упрощенный расчет для macOS через Environment.TickCount64
                    _systemBootTime = DateTime.Now.AddMilliseconds(-Environment.TickCount64);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка получения BootTime: {ex.Message}");
            }

            _systemBootTime = DateTime.Now.AddMilliseconds(-Environment.TickCount64);
        }

        private void UpdateSystemInfo(object? sender, EventArgs e)
        {
            try
            {
                double cpuUsage = GetCpuUsage();
                double ramUsage = GetRamUsage();
                double diskUsage = GetDiskUsage();
                double cpuTemp = GetCpuTemperature();

                Dispatcher.UIThread.Post(() =>
                {
                    UpdateCpuInfo(cpuUsage);
                    UpdateRamInfo(ramUsage);
                    UpdateDiskInfo(diskUsage);
                    UpdateCpuTempInfo(cpuTemp);
                    UpdateProcessCount();
                    UpdateUptime();
                    UpdateSystemLoad(cpuUsage, ramUsage, diskUsage);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"❌ Ошибка обновления: {ex.Message}"); }
        }
        #endregion
        #region Cross-Platform Metric Fetchers
        private double GetCpuUsage()
        {
            try
            {
                if (_isWindows && _cpuCounter != null)
                    return _cpuCounter.NextValue();

                if (_isLinux && File.Exists("/proc/stat"))
                {
                    // Простейший парсинг нагрузки из /proc/stat
                    var lines = File.ReadAllLines("/proc/stat");
                    var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
                    if (cpuLine != null)
                    {
                        var parts = cpuLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        double idle = double.Parse(parts[4]);
                        double total = parts.Skip(1).Take(7).Select(double.Parse).Sum();
                        return Math.Clamp((1.0 - (idle / total)) * 100, 0, 100);
                    }
                }
            }
            catch { }

            return 0;
        }

        private double GetRamUsage()
        {
            try
            {
                if (_isWindows)
                {
                 
                    var info = GC.GetGCMemoryInfo();
                    if (info.TotalAvailableMemoryBytes > 0)
                    {
                        
                        return Math.Clamp((1.0 - ((double)info.HighMemoryLoadThresholdBytes / info.TotalAvailableMemoryBytes)) * 100, 0, 100);
                    }
                }
                else if (_isLinux && File.Exists("/proc/meminfo"))
                {
                    var lines = File.ReadAllLines("/proc/meminfo");
                    double total = 0, available = 0;
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemTotal:")) total = double.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                        if (line.StartsWith("MemAvailable:")) available = double.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    }
                    if (total > 0) return Math.Clamp(((total - available) / total) * 100, 0, 100);
                }
            }
            catch { }

            return 0;
        }

        private double GetDiskUsage()
        {
            try
            {
               
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
                if (drive != null)
                {
                    double used = drive.TotalSize - drive.AvailableFreeSpace;
                    return Math.Clamp((used / drive.TotalSize) * 100, 0, 100);
                }
            }
            catch { }

            return 0;
        }

     
        private double GetCpuTemperature()
        {
            try
            {
                if (_isWindows)
                {
                   
                    using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double tempKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                        return Math.Round((tempKelvin / 10.0) - 273.15, 1); // Перевод из Кельвинов в Цельсии
                    }
                }
                else if (_isLinux)
                {
                   
                    if (File.Exists("/sys/class/thermal/thermal_zone0/temp"))
                    {
                        string raw = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp").Trim();
                        if (double.TryParse(raw, out double temp))
                            return Math.Round(temp / 1000.0, 1);
                    }
                }
            }
            catch { }

            return -1; 
        }
        #endregion

        #region UI Updates
        private void UpdateCpuInfo(double usage)
        {
            var cpuPercentText = this.FindControl<TextBlock>("CpuPercentText");
            var cpuProgressBar = this.FindControl<ProgressBar>("CpuProgressBar");

            if (cpuPercentText != null) cpuPercentText.Text = $"{usage:F1}%";
            if (cpuProgressBar != null)
            {
                cpuProgressBar.Value = usage;
                cpuProgressBar.Foreground = GetProgressBarBrush(usage, 50, 80);
            }
        }

        private void UpdateRamInfo(double usage)
        {
            var ramPercentText = this.FindControl<TextBlock>("RamPercentText");
            var ramProgressBar = this.FindControl<ProgressBar>("RamProgressBar");

            if (ramPercentText != null) ramPercentText.Text = $"{usage:F1}%";
            if (ramProgressBar != null)
            {
                ramProgressBar.Value = usage;
                ramProgressBar.Foreground = GetProgressBarBrush(usage, 60, 85);
            }
        }

        private void UpdateDiskInfo(double usage)
        {
            var diskPercentText = this.FindControl<TextBlock>("DiskPercentText");
            var diskProgressBar = this.FindControl<ProgressBar>("DiskProgressBar");

            if (diskPercentText != null) diskPercentText.Text = $"{usage:F1}%";
            if (diskProgressBar != null)
            {
                diskProgressBar.Value = usage;
                diskProgressBar.Foreground = GetProgressBarBrush(usage, 30, 70);
            }
        }

        private void UpdateCpuTempInfo(double temp)
        {
            var cpuTempText = this.FindControl<TextBlock>("CpuTempText");
            if (cpuTempText == null) return;

            if (temp < 0)
            {
                cpuTempText.Text = "N/A";
                cpuTempText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
            }
            else
            {
                cpuTempText.Text = $"{temp:F1}°C";
                cpuTempText.Foreground = GetProgressBarBrush(temp, 65, 85);
            }
        }

        private void UpdateSystemLoad(double cpu, double ram, double disk)
        {
            var systemLoadPercentText = this.FindControl<TextBlock>("SystemLoadPercentText");
            var systemLoadProgressBar = this.FindControl<ProgressBar>("SystemLoadProgressBar");

            var overall = (cpu + ram + disk) / 3;
            if (systemLoadPercentText != null) systemLoadPercentText.Text = $"{overall:F1}%";
            if (systemLoadProgressBar != null)
            {
                systemLoadProgressBar.Value = overall;
                systemLoadProgressBar.Foreground = GetProgressBarBrush(overall, 30, 70);
            }
        }

        private SolidColorBrush GetProgressBarBrush(double value, double yellowThreshold, double redThreshold)
        {
            if (value < yellowThreshold) return new SolidColorBrush(Color.Parse("#00FF88"));
            if (value < redThreshold) return new SolidColorBrush(Color.Parse("#FFA500"));
            return new SolidColorBrush(Color.Parse("#FF4444"));
        }

        private void UpdateProcessCount()
        {
            var processCountText = this.FindControl<TextBlock>("ProcessCountText");
            if (processCountText == null) return;

            try
            {
               
                var count = Process.GetProcesses().Length;
                processCountText.Text = $"{count} процессов";
                processCountText.Foreground = GetTextBrush(count, 150, 300);
            }
            catch
            {
                processCountText.Text = "Недоступно";
                processCountText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
            }
        }

        private SolidColorBrush GetTextBrush(int value, int yellowThreshold, int redThreshold)
        {
            if (value < yellowThreshold) return new SolidColorBrush(Color.Parse("#00FF88"));
            if (value < redThreshold) return new SolidColorBrush(Color.Parse("#FFA500"));
            return new SolidColorBrush(Color.Parse("#FF4444"));
        }

        private void UpdateUptime()
        {
            var uptimeText = this.FindControl<TextBlock>("UptimeText");
            if (uptimeText == null) return;

            try
            {
                var uptime = DateTime.Now - _systemBootTime;
                uptimeText.Text = uptime.TotalHours switch
                {
                    < 1 => $"{uptime.Minutes} мин",
                    < 24 => $"{uptime.Hours} ч {uptime.Minutes} мин",
                    _ => $"{uptime.Days} д {uptime.Hours} ч"
                };
                uptimeText.Foreground = uptime.TotalHours switch
                {
                    < 1 => new SolidColorBrush(Color.Parse("#FF4444")),
                    < 24 => new SolidColorBrush(Color.Parse("#FFA500")),
                    _ => new SolidColorBrush(Color.Parse("#00FF88"))
                };
            }
            catch
            {
                uptimeText.Text = "Недоступно";
                uptimeText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
            }
        }
        #endregion

        #region Project Statistics
     
        private DispatcherTimer? _projectStatsTimer;

        private void InitProjectStatistics()
        {
            _sessionStartTime = DateTime.Now;

          
            if (SimpleSettings.FirstLaunchDate == DateTime.MinValue)
            {
                SimpleSettings.FirstLaunchDate = DateTime.Now;
            }

            SimpleSettings.LaunchCount++;
            SimpleSettings.SaveSettings();

            _projectStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _projectStatsTimer.Tick += (s, e) => UpdateProjectStatsUI();
            _projectStatsTimer.Start();

            UpdateProjectStatsUI();
        }

        private void UpdateProjectStatsUI()
        {
            try
            {
                // 1. Дни работы (от первого запуска)
                int daysWorking = (DateTime.Now - SimpleSettings.FirstLaunchDate).Days + 1;
                var daysText = this.FindControl<TextBlock>("DaysText");
                if (daysText != null) daysText.Text = $"{daysWorking} дней";

                // 2. Количество запусков
                var launchesText = this.FindControl<TextBlock>("LaunchesText");
                if (launchesText != null) launchesText.Text = $"{SimpleSettings.LaunchCount} раз";

                // 3. Среднее время одной сессии
                double currentSessionMinutes = (DateTime.Now - _sessionStartTime).TotalMinutes;
                double totalMinutes = SimpleSettings.TotalUsageMinutes + currentSessionMinutes;
                double avgMinutes = SimpleSettings.LaunchCount > 0 ? totalMinutes / SimpleSettings.LaunchCount : 0;

                var avgTimeText = this.FindControl<TextBlock>("AvgTimeText");
                if (avgTimeText != null)
                {
                    avgTimeText.Text = avgMinutes < 60
                        ? $"{Math.Round(avgMinutes)} минут"
                        : $"{avgMinutes / 60.0:F1} часов";
                }

                // 4. День рождения
                UpdateBirthdayUI();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка обновления UI статистики: {ex.Message}");
            }
        }

        private void UpdateBirthdayUI()
        {
            var birthdayText = this.FindControl<TextBlock>("BirthdayText");
            var birthdayLetterBtn = this.FindControl<Button>("BirthdayLetterBtn");

            if (SimpleSettings.UserBirthday != DateTime.MinValue)
            {
                if (birthdayText != null)
                    birthdayText.Text = SimpleSettings.UserBirthday.ToString("dd.MM.yyyy");

               
                bool isBirthdayToday = SimpleSettings.UserBirthday.Day == DateTime.Now.Day &&
                                       SimpleSettings.UserBirthday.Month == DateTime.Now.Month;

                if (birthdayLetterBtn != null)
                    birthdayLetterBtn.IsVisible = isBirthdayToday;
            }
        }

       

      
        public void SaveSessionTimeOnExit()
        {
            double sessionMinutes = (DateTime.Now - _sessionStartTime).TotalMinutes;
            SimpleSettings.TotalUsageMinutes += sessionMinutes;
            SimpleSettings.SaveSettings();
        }
        #endregion


        #region System Cleanup
        private async void CleanCache_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            try
            {
                await SetButtonState(button, "⏳ ОЧИСТКА...", false);

                long bytesCleaned = 0;

                await Task.Run(() =>
                {
                    bytesCleaned += CleanTempFiles();
                    bytesCleaned += CleanBrowserCache();
                    CleanRecycleBin();
                });

                double mbCleaned = Math.Round(bytesCleaned / 1024.0 / 1024.0, 2);
                ShowSystemNotification("✅ Очистка завершена", $"Освобождено {mbCleaned} MB", "#00FF88");
            }
            catch (Exception ex)
            {
                ShowSystemNotification("❌ Ошибка очистки", ex.Message, "#FF4444");
            }
            finally
            {
                await SetButtonState(button, "🧹 ОЧИСТКА", true);
            }
        }

        private long CleanTempFiles()
        {
            long cleaned = 0;
            var tempPaths = new List<string> { IoPath.GetTempPath() };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                tempPaths.Add(IoPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                tempPaths.Add("/var/tmp");
            }

            foreach (var path in tempPaths)
            {
                cleaned += CleanDirectory(path);
            }

            return cleaned;
        }

        private long CleanBrowserCache()
        {
            long cleaned = 0;
            var paths = new List<string>();
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                paths.Add(IoPath.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"));
                paths.Add(IoPath.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"));
                paths.Add(IoPath.Combine(localAppData, @"Mozilla\Firefox\Profiles"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                paths.Add(IoPath.Combine(home, ".cache/google-chrome/Default/Cache"));
                paths.Add(IoPath.Combine(home, ".cache/mozilla/firefox"));
                paths.Add(IoPath.Combine(home, ".cache/microsoft-edge"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                paths.Add(IoPath.Combine(home, "Library/Caches/Google/Chrome/Default/Cache"));
                paths.Add(IoPath.Combine(home, "Library/Caches/Firefox"));
                paths.Add(IoPath.Combine(home, "Library/Caches/com.microsoft.edgemac"));
            }

            foreach (var path in paths.Where(Directory.Exists))
            {
                cleaned += CleanDirectory(path);
            }

            return cleaned;
        }

        private long CleanDirectory(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long totalSize = 0;

            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        long size = info.Length;
                        File.Delete(file);
                        totalSize += size;
                    }
                    catch { }
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        totalSize += CleanDirectory(dir);
                        Directory.Delete(dir, true);
                    }
                    catch { }
                }
            }
            catch { }

            return totalSize;
        }

        private void CleanRecycleBin()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null,
                        RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                   
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string trashPath = IoPath.Combine(home, ".local/share/Trash");
                    CleanDirectory(trashPath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Очистка корзины в macOS
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string trashPath = IoPath.Combine(home, ".Trash");
                    CleanDirectory(trashPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка очистки корзины: {ex.Message}");
            }
        }

        [DllImport("Shell32.dll")]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, RecycleFlags flags);

        [Flags]
        private enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }
        #endregion


        #region Diagnostics
        private async void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            try
            {
                await SetButtonState(button, "⏳ ДИАГНОСТИКА...", false);
                var result = await Task.Run(RunSystemDiagnostics);
                ShowDiagnosticsResults(result);
            }
            catch (Exception ex)
            {
                ShowSystemNotification("❌ Ошибка диагностики", ex.Message, "#FF4444");
            }
            finally
            {
                await SetButtonState(button, "📊 ДИАГНОСТИКА", true);
            }
        }

        private string RunSystemDiagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ПОДРОБНАЯ ДИАГНОСТИКА СИСТЕМЫ ===\n");
            AddSystemInfo(sb);
            AddHardwareInfo(sb);
            AddMemoryInfo(sb);
            AddDiskInfo(sb);
            AddNetworkInfo(sb);
            AddProcessInfo(sb);
            return sb.ToString();
        }

        private void AddSystemInfo(StringBuilder sb)
        {
            sb.AppendLine("🖥️ ОПЕРАЦИОННАЯ СИСТЕМА:");
            sb.AppendLine($" Платформа: {RuntimeInformation.OSDescription}");
            sb.AppendLine($" Архитектура ОС: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($" Архитектура процесса: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($" Имя ПК: {Environment.MachineName}");
            sb.AppendLine($" Пользователь: {Environment.UserName}");
            sb.AppendLine($" .NET Runtime: {RuntimeInformation.FrameworkDescription}");

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            sb.AppendLine($" Время работы: {uptime.Days}д {uptime.Hours}ч {uptime.Minutes}м");
        }

        private void AddHardwareInfo(StringBuilder sb)
        {
            sb.AppendLine("\n⚡ ПРОЦЕССОР:");
            sb.AppendLine($" Логических ядер: {Environment.ProcessorCount}");
        }

        private void AddMemoryInfo(StringBuilder sb)
        {
            sb.AppendLine("\n🧠 ОПЕРАТИВНАЯ ПАМЯТЬ:");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    double totalGb = memStatus.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
                    double availGb = memStatus.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
                    double usedGb = totalGb - availGb;

                    sb.AppendLine($" Загрузка: {memStatus.dwMemoryLoad}%");
                    sb.AppendLine($" Использовано: {usedGb:F2} GB");
                    sb.AppendLine($" Свободно: {availGb:F2} GB");
                    sb.AppendLine($" Всего: {totalGb:F2} GB");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/proc/meminfo"))
            {
                try
                {
                    var lines = File.ReadAllLines("/proc/meminfo");
                    double totalKb = 0, availKb = 0;
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemTotal:")) totalKb = double.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                        if (line.StartsWith("MemAvailable:")) availKb = double.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    }

                    double totalGb = totalKb / 1024.0 / 1024.0;
                    double availGb = availKb / 1024.0 / 1024.0;
                    double usedGb = totalGb - availGb;
                    double load = (usedGb / totalGb) * 100;

                    sb.AppendLine($" Загрузка: {load:F1}%");
                    sb.AppendLine($" Использовано: {usedGb:F2} GB");
                    sb.AppendLine($" Свободно: {availGb:F2} GB");
                    sb.AppendLine($" Всего: {totalGb:F2} GB");
                }
                catch { }
            }
            else
            {
                var info = GC.GetGCMemoryInfo();
                double totalGb = info.TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($" Доступно системе: {totalGb:F2} GB");
            }
        }

        private void AddDiskInfo(StringBuilder sb)
        {
            sb.AppendLine("\n💾 НАКОПИТЕЛИ:");
            try
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    double freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    double totalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    double freePercent = (freeGb / totalGb) * 100;

                    sb.AppendLine($" Диск [{drive.Name}] ({drive.DriveFormat})");
                    sb.AppendLine($" Тип: {drive.DriveType}");
                    sb.AppendLine($" Свободно: {freeGb:F1} GB из {totalGb:F1} GB ({freePercent:F1}%)");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($" Ошибка чтения дисков: {ex.Message}");
            }
        }

        private void AddNetworkInfo(StringBuilder sb)
        {
            sb.AppendLine("\n🌐 СЕТЬ И ИНТЕРФЕЙСЫ:");
            try
            {
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    sb.AppendLine($" Интерфейс: {ni.Name} ({ni.NetworkInterfaceType})");
                    var props = ni.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            sb.AppendLine($" IPv4: {ip.Address}");
                        }
                    }
                }

                var connections = System.Net.NetworkInformation.IPGlobalProperties
                    .GetIPGlobalProperties().GetActiveTcpConnections();
                sb.AppendLine($" Активных TCP соединений: {connections.Length}");
            }
            catch
            {
                sb.AppendLine(" Информация о сети недоступна");
            }
        }

        private void AddProcessInfo(StringBuilder sb)
        {
            sb.AppendLine("\n🔄 ТОП-5 ПРОЦЕССОВ ПО ПАМЯТИ:");
            try
            {
                var processes = Process.GetProcesses();
                sb.AppendLine($" Всего процессов: {processes.Length}");

                var topProcesses = processes
                    .Where(p => SafeGetWorkingSet(p) > 0)
                    .OrderByDescending(SafeGetWorkingSet)
                    .Take(5);

                foreach (var p in topProcesses)
                {
                    try
                    {
                        double ramMb = p.WorkingSet64 / 1024.0 / 1024.0;
                        sb.AppendLine($" • {p.ProcessName} (PID: {p.Id}): {ramMb:F1} MB");
                    }
                    catch { }
                }
            }
            catch { }
        }

        private long SafeGetWorkingSet(Process p)
        {
            try { return p.WorkingSet64; } catch { return 0; }
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
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
            public MEMORYSTATUSEX() => dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
        #endregion


        #region UI Helpers
        private async Task SetButtonState(Button? button, string content, bool enabled)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (button != null)
                {
                    button.Content = content;
                    button.IsEnabled = enabled;
                }
            });
        }

        private void ShowDiagnosticsResults(string results)
        {
            var dialog = new Window
            {
                Title = "📊 Результаты диагностики",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = results,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(15)
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
                dialog.ShowDialog(mainWindow);
            else
                dialog.Show();
        }

        private void LoadCurrentSettings()
        {
            CheckAutoStartStatus();
            var autoStartCheckBox = this.FindControl<CheckBox>("AutoStartCheckBox");
            var minimizeToTrayCheckBox = this.FindControl<CheckBox>("MinimizeToTrayCheckBox");

            if (autoStartCheckBox != null) autoStartCheckBox.IsChecked = SimpleSettings.AutoStart;
            if (minimizeToTrayCheckBox != null) minimizeToTrayCheckBox.IsChecked = SimpleSettings.MinimizeToTray;
        }

        private void OnLanguageChanged() => ForceRefreshBindings();

        private void UserControl_Loaded(object? sender, RoutedEventArgs e) { }
        private void UserControl_Unloaded(object? sender, RoutedEventArgs e) { }
        #endregion

        #region Chat Methods
        private void SendChatButton_Click(object? sender, RoutedEventArgs e)
        {
            var chatTextBox = this.FindControl<TextBox>("ChatTextBox");
            if (chatTextBox == null) return;

            var message = chatTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(message)) return;

            AddUserMessage(message);
            chatTextBox.Text = "";
            StartAIResponse();
        }

        private void AddUserMessage(string message)
        {
            var chatMessagesPanel = this.FindControl<StackPanel>("ChatMessagesPanel")
                ?? (Control?)this.FindControl<Panel>("ChatMessagesPanel");
            if (chatMessagesPanel == null) return;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#408040")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(40, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "👤 Вы", Foreground = new SolidColorBrush(Colors.LightGreen), FontSize = 9, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 4) });
            stack.Children.Add(new TextBlock { Text = message, Foreground = Brushes.White, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH:mm"), Foreground = new SolidColorBrush(Colors.LightGreen), FontSize = 8, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) });
            border.Child = stack;

            if (chatMessagesPanel is Panel panel)
                panel.Children.Add(border);

            ScrollToBottom();
        }

        private void AddAIMessage(string message)
        {
            var chatMessagesPanel = this.FindControl<Panel>("ChatMessagesPanel");
            if (chatMessagesPanel == null) return;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#4050B0")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 40, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var messageText = new TextBlock { Text = "", Foreground = Brushes.White, FontSize = 11, TextWrapping = TextWrapping.Wrap };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "🤖 AI Assistant", Foreground = new SolidColorBrush(Colors.LightBlue), FontSize = 9, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 4) });
            stack.Children.Add(messageText);
            stack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH:mm"), Foreground = new SolidColorBrush(Colors.LightBlue), FontSize = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) });
            border.Child = stack;

            chatMessagesPanel.Children.Add(border);
            ScrollToBottom();

            ShowTextGradually(message, messageText);
        }

        private void StartAIResponse()
        {
            if (_isAITyping) return;
            _isAITyping = true;
            var response = _aiResponses[_random.Next(_aiResponses.Length)];

            var typingIndicator = this.FindControl<Control>("TypingIndicator");
            if (typingIndicator != null) typingIndicator.IsVisible = true;

            _currentTypingStep = 0;
            _typingTimer.Start();

            Task.Delay(2000).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _typingTimer.Stop();
                    if (typingIndicator != null) typingIndicator.IsVisible = false;
                    AddAIMessage(response);
                    _isAITyping = false;
                });
            });
        }

        private void ShowTextGradually(string fullText, TextBlock targetBlock)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            var currentLength = 0;

            timer.Tick += (s, ev) =>
            {
                if (currentLength < fullText.Length)
                {
                    targetBlock.Text = fullText.Substring(0, ++currentLength);
                    ScrollToBottom();
                }
                else
                {
                    timer.Stop();
                }
            };

            targetBlock.Text = "";
            timer.Start();
        }

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _currentTypingStep = (_currentTypingStep + 1) % 4;
            var typingDots = this.FindControl<TextBlock>("TypingDots");
            if (typingDots != null)
            {
                typingDots.Text = _currentTypingStep switch { 1 => ".", 2 => "..", 3 => "...", _ => "" };
            }
        }

        private void ScrollToBottom()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var chatScrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");
                chatScrollViewer?.ScrollToEnd();
            });
        }
        #endregion

        #region About Methods
        

        private void AboutMe_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var aboutWindow = new AboutDeveloperWindow();
                var mainWindow = GetMainWindow();
                if (mainWindow != null) aboutWindow.ShowDialog(mainWindow);
                else aboutWindow.Show();
            }
            catch (Exception ex)
            {
                ShowSystemNotification("❌ Ошибка", $"Ошибка при открытии информации: {ex.Message}", "#FF4444");
            }
        }
        #endregion

        #region Birthday Methods
        private void BirthdayLetterBtn_Click(object? sender, RoutedEventArgs e)
        {
            var birthdayWindow = new BirthdayLetterWindow();
            var mainWindow = GetMainWindow();
            if (mainWindow != null) birthdayWindow.ShowDialog(mainWindow);
            else birthdayWindow.Show();
        }

        private void SelectBirthdayBtn_Click(object? sender, RoutedEventArgs e)
        {
            var window = new Window
            {
                Title = "🎂 Выбери день рождения",
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            var datePicker = new DatePicker
            {
                SelectedDate = DateTimeOffset.Now.AddYears(-20),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var saveButton = new Button
            {
                Content = "🎉 Сохранить!",
                Background = new SolidColorBrush(Color.Parse("#FFD700")),
                Foreground = Brushes.Black
            };

            saveButton.Click += (s, args) =>
            {
                if (datePicker.SelectedDate.HasValue)
                {
                    SaveBirthday(datePicker.SelectedDate.Value.DateTime);
                    window.Close();
                }
            };

            stack.Children.Add(new TextBlock
            {
                Text = "Когда у тебя день рождения?",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });
            stack.Children.Add(datePicker);
            stack.Children.Add(saveButton);

            window.Content = stack;

            var mainWindow = GetMainWindow();
            if (mainWindow != null) window.ShowDialog(mainWindow);
            else window.Show();
        }

        private void SaveBirthday(DateTime birthday)
        {
            SimpleSettings.SaveBirthday(birthday);

            var birthdayText = this.FindControl<TextBlock>("BirthdayText");
            var selectBirthdayBtn = this.FindControl<Button>("SelectBirthdayBtn");
            var birthdayLetterBtn = this.FindControl<Button>("BirthdayLetterBtn");

            if (birthdayText != null) birthdayText.Text = $"{birthday:dd MMMM} 🎉";
            if (selectBirthdayBtn != null) selectBirthdayBtn.IsVisible = false;
            if (birthdayLetterBtn != null) birthdayLetterBtn.IsVisible = IsBirthdayToday(birthday);
        }

        private void CheckSavedBirthday()
        {
            var saved = SimpleSettings.UserBirthday;
            if (saved != DateTime.MinValue)
            {
                var birthdayText = this.FindControl<TextBlock>("BirthdayText");
                var selectBirthdayBtn = this.FindControl<Button>("SelectBirthdayBtn");
                var birthdayLetterBtn = this.FindControl<Button>("BirthdayLetterBtn");

                if (birthdayText != null) birthdayText.Text = $"{saved:dd MMMM} 🎉";
                if (selectBirthdayBtn != null) selectBirthdayBtn.IsVisible = false;
                if (IsBirthdayToday(saved) && birthdayLetterBtn != null)
                    birthdayLetterBtn.IsVisible = true;
            }
        }

        private bool IsBirthdayToday(DateTime birthday) =>
            birthday.Month == DateTime.Now.Month && birthday.Day == DateTime.Now.Day;
        #endregion

        #region Process Methods
        private void ShowProcesses_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
                    ShowSystemNotification("🔍 Диспетчер задач", "Запущен системный диспетчер задач", "#87CEEB");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", "-a 'Activity Monitor'");
                    ShowSystemNotification("🔍 Мониторинг системы", "Запущен Activity Monitor", "#87CEEB");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (!TryStartLinuxSystemMonitor())
                    {
                        ShowSystemNotification("⚠️ Внимание", "Системный монитор не найден в системе", "#FFA500");
                        return;
                    }
                    ShowSystemNotification("🔍 Монитор процессов", "Запущен системный монитор", "#87CEEB");
                }
            }
            catch (Exception ex)
            {
                ShowSystemNotification("❌ Ошибка", "Не удалось открыть монитор процессов: " + ex.Message, "#FF4444");
            }
        }

       
        private bool TryStartLinuxSystemMonitor()
        {
            string[] monitors = { "gnome-system-monitor", "ksysguard", "mate-system-monitor", "qps" };

            foreach (var monitor in monitors)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = monitor,
                        UseShellExecute = true
                    });
                    return true;
                }
                catch { }
            }

          
            string[] terminals = { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal" };
            foreach (var term in terminals)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = term,
                        Arguments = "-e htop",
                        UseShellExecute = true
                    });
                    return true;
                }
                catch { }
            }

            return false;
        }
        #endregion


        #region Calendar Methods
        private void OpenCalendar_Click(object? sender, RoutedEventArgs e)
        {
            var calendarWindow = new CalendarWindow();
            var mainWindow = GetMainWindow();

            if (mainWindow != null)
                calendarWindow.ShowDialog(mainWindow);
            else
                calendarWindow.Show();
        }
        #endregion

        #region Animations
       

        
        private void InitSettingsAnimations()
        {
            var headerBorder = this.FindControl<Border>("HeaderBorder");
            if (headerBorder != null)
            {
                
                headerBorder.PointerEntered += Border_PointerEntered;
                headerBorder.PointerExited += Border_PointerExited;
            }

          
            UpdateSeasonalGradient();

           
            _rotationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _rotationTimer.Tick += RotationTimer_Tick;
            _rotationTimer.Start();

         
            CreateStars();
            CreateParticles();
            StartShootingStarGenerator();
        }

        private void UpdateSeasonalGradient()
        {
            try
            {
                var headerAnimatedBorder = this.FindControl<Border>("HeaderAnimatedBorder");
                if (headerAnimatedBorder == null) return;

                var hour = DateTime.Now.Hour;
                var month = DateTime.Now.Month;

               
                var palette = GetRichPalette(hour, month);

                var advancedBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
            {
                new GradientStop(palette.Item1, 0.0),
                new GradientStop(palette.Item2, 0.25),
                new GradientStop(palette.Item3, 0.5),
                new GradientStop(palette.Item4, 0.75),
                new GradientStop(palette.Item1, 1.0) 
            }
                };

                headerAnimatedBorder.BorderBrush = advancedBrush;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка генерации красивого градиента: {ex.Message}");
            }
        }
        private (Color, Color, Color, Color) GetRichPalette(int hour, int month)
        {
            Color c1, c2, c3, c4;

            // 1. Четкая сезонная палитра (без случайных цветов)
            if (month == 12 || month == 1 || month == 2) // Зима: Лед, серебро, глубокий синий
            {
                c1 = Color.Parse("#00F2FE"); // Яркий ледяной циан
                c2 = Color.Parse("#4FACFE"); // Голубой
                c3 = Color.Parse("#6a11cb"); // Холодный глубокий синий
                c4 = Color.Parse("#2575fc"); // Насыщенный синий
            }
            else if (month >= 3 && month <= 5) // Весна: Зелень, мята, нежный розовый
            {
                c1 = Color.Parse("#00b09b"); // Мята
                c2 = Color.Parse("#96c93d"); // Весенняя листва
                c3 = Color.Parse("#ff9a9e"); // Розовый бутон
                c4 = Color.Parse("#fecfef"); // Светлый пастельно-розовый
            }
            else if (month >= 6 && month <= 8) // Лето: Закат, золото, теплое солнце
            {
                c1 = Color.Parse("#f12711"); // Теплый красный закат
                c2 = Color.Parse("#f5af19"); // Золотое солнце
                c3 = Color.Parse("#ff4b1f"); // Оранжевый
                c4 = Color.Parse("#ffe000"); // Яркий желтый
            }
            else // Осень: Янтарный, бронза, глубокий фиолетовый
            {
                c1 = Color.Parse("#D4145A"); // Осенняя листва (пурпурный)
                c2 = Color.Parse("#FBB03B"); // Янтарно-золотой
                c3 = Color.Parse("#8A2BE2"); // Фиолетовая тень
                c4 = Color.Parse("#FF4E50"); // Огненный
            }

            // 2. Корректировка под время суток (делаем ночью темнее и неоновее, днем — ярче)
            if (hour < 6 || hour >= 21) // Ночь: добавляем мистический темный оттенок
            {
                c3 = Color.Parse("#1a0033");
            }
            else if (hour >= 6 && hour < 12) // Утро: свежие светлые нотки
            {
                c4 = Color.Parse("#E0FFFF"); // Легкий светлый аквамарин
            }

            return (c1, c2, c3, c4);
        }

        private void RotationTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var headerAnimatedBorder = this.FindControl<Border>("HeaderAnimatedBorder");
                if (headerAnimatedBorder != null && headerAnimatedBorder.Opacity > 0)
                {
                   
                    _rotationAngle = (_rotationAngle + 1.5) % 360;

                    if (headerAnimatedBorder.BorderBrush is LinearGradientBrush gradient)
                    {
                        gradient.Transform = new RotateTransform(_rotationAngle);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка ротации: {ex.Message}");
            }
        }

        private (Color, Color, Color) GetTimeColors(int hour) => hour switch
        {
            >= 6 and < 12 => (Colors.Yellow, Colors.HotPink, Colors.LightCyan),
            >= 12 and < 18 => (Colors.LightYellow, Colors.LightSkyBlue, Colors.White),
            >= 18 and < 23 => (Colors.Violet, Colors.OrangeRed, Colors.Gold),
            _ => (Color.Parse("#00D2FF"), Color.Parse("#8A2BE2"), Colors.White)
        };

        private (Color, Color, Color) GetSeasonColors(int month) => month switch
        {
            12 or 1 or 2 => (Colors.White, Colors.Cyan, Color.Parse("#00FF9D")),
            3 or 4 or 5 => (Colors.LightGreen, Colors.Pink, Colors.RosyBrown),
            6 or 7 or 8 => (Colors.GreenYellow, Colors.Azure, Colors.MintCream),
            _ => (Colors.DarkOrange, Colors.DarkRed, Colors.Yellow)
        };

        private Color MixColors(Color c1, Color c2, double ratio) => Color.FromArgb(
            255,
            (byte)Math.Min(255, c1.R * ratio + c2.R * (1 - ratio) + 30),
            (byte)Math.Min(255, c1.G * ratio + c2.G * (1 - ratio) + 30),
            (byte)Math.Min(255, c1.B * ratio + c2.B * (1 - ratio) + 30)
        );

        private void Border_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            AnimateBorder(1, 35, 0.5);
        }

        private void Border_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            AnimateBorder(0, 25, 0.3);
        }

        private void AnimateBorder(double opacity, double blurRadius, double shadowOpacity)
        {
            var headerAnimatedBorder = this.FindControl<Border>("HeaderAnimatedBorder");
            var headerBorder = this.FindControl<Border>("HeaderBorder");

            if (headerAnimatedBorder != null)
                headerAnimatedBorder.Opacity = opacity;

            if (headerBorder?.Effect is DropShadowEffect shadow)
            {
                shadow.BlurRadius = blurRadius;
                shadow.Opacity = shadowOpacity;
            }
        }

        private void CreateStars()
        {
            var starsLayer = this.FindControl<Canvas>("StarsLayer");
            if (starsLayer == null) return;
            starsLayer.Children.Clear();

            for (int i = 0; i < 200; i++)
                CreateStar(1, 2, 0.2, 0.6);
            for (int i = 0; i < 50; i++)
                CreateStar(2, 4, 0.5, 0.9);
        }

        private void CreateStar(double minSize, double maxSize, double minOpacity, double maxOpacity)
        {
            var starsLayer = this.FindControl<Canvas>("StarsLayer");
            if (starsLayer == null) return;

            var size = _random.Next((int)(minSize * 10), (int)(maxSize * 10)) / 10.0;
            var opacity = _random.Next((int)(minOpacity * 10), (int)(maxOpacity * 10)) / 10.0;

            var star = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Opacity = opacity
            };

            Canvas.SetLeft(star, _random.Next(0, (int)(starsLayer.Bounds.Width > 0 ? starsLayer.Bounds.Width : 800)));
            Canvas.SetTop(star, _random.Next(0, (int)(starsLayer.Bounds.Height > 0 ? starsLayer.Bounds.Height : 600)));
            starsLayer.Children.Add(star);
        }

        private void CreateParticles()
        {
            var particlesLayer = this.FindControl<Canvas>("ParticlesLayer");
            if (particlesLayer == null) return;

            particlesLayer.Children.Clear();
            for (int i = 0; i < 60; i++)
            {
                var particle = new Ellipse
                {
                    Width = _random.Next(1, 3),
                    Height = _random.Next(1, 3),
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)_random.Next(80, 180),
                        (byte)_random.Next(100, 255),
                        (byte)_random.Next(150, 255),
                        (byte)_random.Next(200, 255)
                    )),
                    Opacity = _random.Next(3, 8) / 10.0
                };
                Canvas.SetLeft(particle, _random.Next(0, (int)(particlesLayer.Bounds.Width > 0 ? particlesLayer.Bounds.Width : 800)));
                Canvas.SetTop(particle, _random.Next(0, (int)(particlesLayer.Bounds.Height > 0 ? particlesLayer.Bounds.Height : 600)));
                particlesLayer.Children.Add(particle);
            }
        }

        private void StartShootingStarGenerator()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => CreateShootingStar();
            timer.Start();
        }

        private void CreateShootingStar()
        {
            var shootingStarsLayer = this.FindControl<Canvas>("ShootingStarsLayer");
            if (_shootingStars.Count >= 2 || shootingStarsLayer == null) return;

            var star = new Line
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(25, 0),
                Stroke = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops = new GradientStops
            {
                new GradientStop(Colors.White, 0),
                new GradientStop(Color.FromArgb(150, 0, 255, 255), 0.5),
                new GradientStop(Colors.Transparent, 1)
            }
                },
                StrokeThickness = 2,
                Opacity = 0.8,
                RenderTransform = new RotateTransform(30)
            };

            var startX = _random.Next(200, 860);
            var startY = _random.Next(50, 300);

            Canvas.SetLeft(star, startX);
            Canvas.SetTop(star, startY);

            shootingStarsLayer.Children.Add(star);
            _shootingStars.Add(star);

            AnimateShootingStar(star, startX, startY);
        }

        private void AnimateShootingStar(Line star, double startX, double startY)
        {
            Task.Run(async () =>
            {
                for (int i = 0; i < 60; i++)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Canvas.SetLeft(star, startX + (i * 5));
                        Canvas.SetTop(star, startY + (i * 2.5));
                        star.Opacity = 0.8 - (i / 60.0);
                    });
                    await Task.Delay(20);
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var shootingStarsLayer = this.FindControl<Canvas>("ShootingStarsLayer");
                    shootingStarsLayer?.Children.Remove(star);
                    _shootingStars.Remove(star);
                });
            });
        }
        #endregion

        #region Additional UI Methods
        private void ShowStatsNotification(string title, string message, string colorHex)
        {
            ShowSystemNotification(title, message, colorHex);
        }

        private void RegisterButton_Click(object? sender, RoutedEventArgs e) { }
        private void RateButton_Click(object? sender, RoutedEventArgs e) { }
        private void ShowRatingWindow() { }
        private void AnimationsCheckBox_Changed(object? sender, RoutedEventArgs e) 
        { 
           if (sender is CheckBox check)
            {
                SimpleSettings.EnableSeasonalAnimations = check.IsChecked ?? false;

                SimpleSettings.SaveSettings();
                SimpleSettings.ApplyAllSettings();
            }
        }
        private void StarsCheckBox_Changed(object? sender, RoutedEventArgs e) { }
        #endregion
    }
}