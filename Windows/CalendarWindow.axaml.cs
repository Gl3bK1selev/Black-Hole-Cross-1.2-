using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Black_Hole_Cross
{
    public partial class CalendarWindow : Window
    {
        private DateTime _currentMonth;
        private readonly List<Holiday> _holidays;
        private CalendarDay? _selectedDay;
        private DateTime? _selectedDate;
        private Dictionary<string, string> _customEvents = new();
        private readonly string _eventsFilePath;
        private bool _isMaximized = false;
        private PixelPoint _savedPosition;
        private Size _savedSize;
        private List<SystemNotification> _pendingNotifications = new();

        public CalendarWindow()
        {
            InitializeComponent();
            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "BlackHoleCross");
            Directory.CreateDirectory(folder);
            _eventsFilePath = Path.Combine(folder, "calendar_custom_events.json");

            LoadCustomEvents();
            _holidays = CreateHolidays();
            UpdateCalendar();

            // Показываем уведомления о сегодняшних событиях при запуске
            Dispatcher.UIThread.InvokeAsync(() => CheckTodayNotifications(), DispatcherPriority.Background);

            // Анимация появления
            Opacity = 0;
            var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            double opacity = 0;
            fadeTimer.Tick += (s, e) =>
            {
                opacity += 0.04;
                Opacity = Math.Min(opacity, 1);
                if (opacity >= 1) fadeTimer.Stop();
            };
            fadeTimer.Start();
        }

        private void LoadCustomEvents()
        {
            try
            {
                if (File.Exists(_eventsFilePath))
                {
                    string json = File.ReadAllText(_eventsFilePath);
                    _customEvents = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch { _customEvents = new(); }
        }

        private void SaveCustomEventsToFile()
        {
            try
            {
                string json = JsonSerializer.Serialize(_customEvents);
                File.WriteAllText(_eventsFilePath, json);
            }
            catch { }
        }

        private void CheckTodayNotifications()
        {
            string todayKey = DateTime.Today.ToString("yyyy-MM-dd");
            var notifications = new List<SystemNotification>();

            // Проверяем кастомное напоминание на сегодня
            if (_customEvents.TryGetValue(todayKey, out string? customText) && !string.IsNullOrWhiteSpace(customText))
            {
                notifications.Add(new SystemNotification
                {
                    Title = "📌 Напоминание!",
                    Message = customText,
                    Icon = "📌",
                    Type = NotificationType.Success
                });
            }

            // Проверяем встроенные праздники на сегодня
            var todayHolidays = _holidays.Where(h => h.Date.Date == DateTime.Today).ToList();
            foreach (var holiday in todayHolidays)
            {
                notifications.Add(new SystemNotification
                {
                    Title = $"🎉 {holiday.Name}",
                    Message = holiday.Description,
                    Icon = holiday.Icon,
                    Type = NotificationType.Info
                });
            }

            // Сохраняем уведомления для последовательного показа
            _pendingNotifications = notifications;

            // Показываем первое уведомление сразу
            if (notifications.Any())
            {
                ShowNextNotification();
            }
        }

        private void ShowNextNotification()
        {
            if (_pendingNotifications == null || !_pendingNotifications.Any())
                return;

            var notification = _pendingNotifications.First();
            _pendingNotifications.RemoveAt(0);

            ShowNotification(notification, () =>
            {
                // После закрытия одного уведомления показываем следующее
                Dispatcher.UIThread.InvokeAsync(() => ShowNextNotification(), DispatcherPriority.Background);
            });
        }

        private void ShowNotification(SystemNotification notification, Action? onClosed = null)
        {
            try
            {
                var notificationWindow = new NotificationWindow();

                // Подписываемся на событие закрытия
                notificationWindow.Closed += (s, e) =>
                {
                    onClosed?.Invoke();
                };

                notificationWindow.ShowNotification(notification);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка показа уведомления: {ex.Message}");
                // Если ошибка, показываем следующее уведомление
                onClosed?.Invoke();
            }
        }

        private void TriggerNotification(string title, string message, string icon, NotificationType type)
        {
            try
            {
                var notification = new SystemNotification
                {
                    Title = title,
                    Message = message,
                    Icon = icon,
                    Type = type
                };

                var notificationWindow = new NotificationWindow();
                notificationWindow.ShowNotification(notification);
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        private List<Holiday> CreateHolidays()
        {
            var year = DateTime.Now.Year;
            return new List<Holiday>
            {
                new() { Name = "Новый год", Date = new DateTime(year, 1, 1), Description = "Праздник начала нового года", Icon = "🎆", Style = "NewYearDay" },
                new() { Name = "Рождество", Date = new DateTime(year, 1, 7), Description = "Православное Рождество", Icon = "🌟", Style = "ReligiousDay" },
                new() { Name = "Старый Новый год", Date = new DateTime(year, 1, 14), Description = "Праздник по старому стилю", Icon = "🎄", Style = "NewYearDay" },
                new() { Name = "День защитника Отечества", Date = new DateTime(year, 2, 23), Description = "Праздник всех защитников", Icon = "🪖", Style = "MilitaryDay" },
                new() { Name = "Международный женский день", Date = new DateTime(year, 3, 8), Description = "Праздник весны и женщин", Icon = "🌸", Style = "FunDay" },
                new() { Name = "День смеха", Date = new DateTime(year, 4, 1), Description = "Праздник шуток и розыгрышей", Icon = "😄", Style = "FunDay" },
                new() { Name = "День космонавтики", Date = new DateTime(year, 4, 12), Description = "Первый полет человека в космос", Icon = "🚀", Style = "SpaceDay" },
                new() { Name = "День Победы", Date = new DateTime(year, 5, 9), Description = "Праздник Победы в ВОВ", Icon = "🎗️", Style = "MilitaryDay" },
                new() { Name = "День защиты детей", Date = new DateTime(year, 6, 1), Description = "Международный день детей", Icon = "🧒", Style = "FunDay" },
                new() { Name = "День рождения Telegram", Date = new DateTime(year, 8, 14), Description = "День создания мессенджера", Icon = "✈️", Style = "TechDay" },
                new() { Name = "День знаний", Date = new DateTime(year, 9, 1), Description = "Начало учебного года", Icon = "🎓", Style = "TechDay" },
                new() { Name = "День создания Black Hole", Date = new DateTime(year, 9, 1), Description = "День рождения приложения", Icon = "🌌", Style = "SpaceDay" },
                new() { Name = "День рождения создателя", Date = new DateTime(year, 10, 6), Description = "День рождения разработчика", Icon = "👨‍💻", Style = "BirthdayDay" },
                new() { Name = "День рождения Black Hole", Date = new DateTime(year, 11, 15), Description = "День создания бренда", Icon = "🕳️", Style = "BirthdayDay" },
                new() { Name = "День кофе", Date = new DateTime(year, 4, 17), Description = "Международный день кофе", Icon = "☕", Style = "FunDay" },
                new() { Name = "День книги", Date = new DateTime(year, 4, 23), Description = "Всемирный день книги", Icon = "📚", Style = "TechDay" },
                new() { Name = "День Земли", Date = new DateTime(year, 4, 22), Description = "Международный день матери-Земли", Icon = "🌍", Style = "SpaceDay" },
                new() { Name = "День программиста", Date = new DateTime(year, 9, 13), Description = "Профессиональный праздник программистов", Icon = "💻", Style = "TechDay" }
            };
        }

        private void UpdateCalendar()
        {
            var monthText = this.Find<TextBlock>("CurrentMonthText");
            if (monthText != null)
                monthText.Text = $"{GetLocalizedMonthName(_currentMonth.Month)} {_currentMonth.Year}";

            var days = new List<CalendarDay>();
            DateTime firstDay = new(_currentMonth.Year, _currentMonth.Month, 1);
            int firstDayOffset = (int)firstDay.DayOfWeek - 1;
            if (firstDayOffset < 0) firstDayOffset = 6;

            DateTime startDate = firstDay.AddDays(-firstDayOffset);

            for (int i = 0; i < 42; i++)
            {
                DateTime currentDay = startDate.AddDays(i);
                string dateKey = currentDay.ToString("yyyy-MM-dd");

                var dayHolidays = _holidays.Where(h => h.Date.Date == currentDay.Date).ToList();
                var mainHoliday = dayHolidays.FirstOrDefault();
                bool hasCustomEvent = _customEvents.TryGetValue(dateKey, out string? customTitle) && !string.IsNullOrWhiteSpace(customTitle);

                string icon = "";
                string title = "";
                string desc = "";
                string style = currentDay.Month == _currentMonth.Month ? "NormalDay" : "OtherMonthDay";

                if (hasCustomEvent)
                {
                    icon = "📌";
                    title = customTitle!;
                    desc = $"Пользовательское напоминание: {customTitle}";
                    style = "BirthdayDay";
                }
                else if (mainHoliday != null)
                {
                    icon = mainHoliday.Icon;
                    title = mainHoliday.Name;
                    desc = mainHoliday.Description;
                    style = mainHoliday.Style;
                }

                if (currentDay.Date == DateTime.Today)
                    style = "TodayDay";

                var isToday = currentDay.Date == DateTime.Today;
                var isSelected = _selectedDate.HasValue && currentDay.Date == _selectedDate.Value.Date;

                var calendarDay = new CalendarDay
                {
                    Date = currentDay,
                    DayNumber = currentDay.Day.ToString(),
                    IsCurrentMonth = currentDay.Month == _currentMonth.Month,
                    HolidayIcon = icon,
                    DayStyle = isSelected ? "SelectedDay" : style,
                    Tooltip = !string.IsNullOrEmpty(title) ? $"{title}\n{currentDay:dd MMMM yyyy}" : currentDay.ToString("dd MMMM yyyy"),
                    AdditionalInfo = desc,
                    TextColor = (isToday || isSelected) ? "#FFFFFF" : (currentDay.Month == _currentMonth.Month ? "#E0E0E0" : "#666666")
                };

                days.Add(calendarDay);
            }

            var calendarGrid = this.Find<ItemsControl>("CalendarGrid");
            if (calendarGrid != null)
                calendarGrid.ItemsSource = days;
        }

        private string GetLocalizedMonthName(int month)
        {
            var months = new Dictionary<int, string>
            {
                {1, "Январь"}, {2, "Февраль"}, {3, "Март"}, {4, "Апрель"},
                {5, "Май"}, {6, "Июнь"}, {7, "Июль"}, {8, "Август"},
                {9, "Сентябрь"}, {10, "Октябрь"}, {11, "Ноябрь"}, {12, "Декабрь"}
            };
            return months.GetValueOrDefault(month, "Unknown");
        }

        // Обработчики событий
        public void DayBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is CalendarDay day)
            {
                _selectedDay = day;
                _selectedDate = day.Date;
                string dateKey = day.Date.ToString("yyyy-MM-dd");

                var holidays = _holidays.Where(h => h.Date.Date == day.Date.Date).ToList();
                bool hasCustom = _customEvents.TryGetValue(dateKey, out string? customText) && !string.IsNullOrWhiteSpace(customText);

                var inputTextBox = this.Find<TextBox>("EventInputBox");
                if (inputTextBox != null)
                {
                    inputTextBox.Text = hasCustom ? customText : (holidays.FirstOrDefault()?.Name ?? "");
                }

                if (holidays.Any() || hasCustom)
                {
                    string displayIcon = hasCustom ? "📌" : holidays.First().Icon;
                    string displayName = hasCustom ? customText! : holidays.First().Name;
                    string displayDesc = hasCustom ? $"Личное напоминание на {day.Date:dd.MM.yyyy}" : holidays.First().Description;

                    ShowHolidayInfo(displayIcon, displayName, day.Date, displayDesc);
                }
                else
                {
                    HideHolidayInfo();
                }

                UpdateCalendar();
            }
        }

        private void ShowHolidayInfo(string icon, string name, DateTime date, string description)
        {
            var infoPanel = this.Find<Border>("HolidayInfoPanel");
            if (infoPanel == null) return;

            infoPanel.IsVisible = true;
            infoPanel.Opacity = 1;

            var iconText = this.Find<TextBlock>("HolidayIcon");
            var nameText = this.Find<TextBlock>("HolidayName");
            var dateText = this.Find<TextBlock>("HolidayDate");
            var descText = this.Find<TextBlock>("HolidayDescription");
            var progressBar = this.Find<ProgressBar>("HolidayProgress");
            var daysText = this.Find<TextBlock>("DaysUntilText");

            if (iconText != null) iconText.Text = icon;
            if (nameText != null) nameText.Text = name;
            if (dateText != null) dateText.Text = date.ToString("dd MMMM yyyy");
            if (descText != null) descText.Text = description;

            int daysUntil = (date.Date - DateTime.Today).Days;
            if (daysText != null)
            {
                daysText.Text = daysUntil switch
                {
                    0 => "🎉 Сегодня!",
                    1 => "✨ Завтра",
                    > 0 => $"📅 Через {daysUntil} дн.",
                    _ => $"⏳ Прошло {Math.Abs(daysUntil)} дн."
                };
            }

            if (progressBar != null)
            {
                if (daysUntil > 0)
                {
                    DateTime yearStart = new(DateTime.Now.Year, 1, 1);
                    double daysPassed = (date - yearStart).TotalDays;
                    progressBar.Value = Math.Min(daysPassed / 365.0 * 100, 100);
                }
                else
                {
                    progressBar.Value = 100;
                }
            }
        }

        private void HideHolidayInfo()
        {
            var infoPanel = this.Find<Border>("HolidayInfoPanel");
            if (infoPanel != null)
            {
                infoPanel.IsVisible = false;
                infoPanel.Opacity = 0;
            }
        }

        private void SaveEventBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedDay == null) return;

            string dateKey = _selectedDay.Date.ToString("yyyy-MM-dd");
            var inputTextBox = this.Find<TextBox>("EventInputBox");
            string text = inputTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(text))
            {
                if (_customEvents.ContainsKey(dateKey))
                    _customEvents.Remove(dateKey);
            }
            else
            {
                _customEvents[dateKey] = text;
            }

            SaveCustomEventsToFile();
            UpdateCalendar();

            if (!string.IsNullOrEmpty(text))
            {
                ShowHolidayInfo("📌", text, _selectedDay.Date, $"Пользовательское напоминание на {_selectedDay.Date:dd.MM.yyyy}");
                TriggerNotification("✅ Сохранено", $"Напоминание на {_selectedDay.Date:dd.MM.yyyy} добавлено!", "💾", NotificationType.Success);
            }
            else
            {
                HideHolidayInfo();
                TriggerNotification("🗑️ Удалено", "Напоминание удалено", "🗑️", NotificationType.Warning);
            }
        }

        private void DeleteEventBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedDay == null) return;

            string dateKey = _selectedDay.Date.ToString("yyyy-MM-dd");
            if (_customEvents.ContainsKey(dateKey))
            {
                _customEvents.Remove(dateKey);
                SaveCustomEventsToFile();

                var inputTextBox = this.Find<TextBox>("EventInputBox");
                if (inputTextBox != null) inputTextBox.Text = "";

                UpdateCalendar();
                HideHolidayInfo();
                TriggerNotification("🗑️ Удалено", "Напоминание удалено", "🗑️", NotificationType.Warning);
            }
        }

        private void DismissNotification_Click(object? sender, RoutedEventArgs e)
        {
            HideHolidayInfo();
        }

        private void PrevMonthBtn_Click(object? sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateCalendar();
        }

        private void NextMonthBtn_Click(object? sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateCalendar();
        }

        private void TodayBtn_Click(object? sender, RoutedEventArgs e)
        {
            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _selectedDate = DateTime.Today;
            UpdateCalendar();
            HideHolidayInfo();

            var inputTextBox = this.Find<TextBox>("EventInputBox");
            if (inputTextBox != null) inputTextBox.Text = "";
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_isMaximized)
            {
                WindowState = WindowState.Normal;
                Position = _savedPosition;
                Width = _savedSize.Width;
                Height = _savedSize.Height;
                _isMaximized = false;
            }
            else
            {
                _savedPosition = Position;
                _savedSize = new Size(Width, Height);
                WindowState = WindowState.Maximized;
                _isMaximized = true;
            }
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (WindowState == WindowState.Maximized)
                {
                    return;
                }
                BeginMoveDrag(e);
            }
        }
    }

    public class Holiday
    {
        public string Name { get; set; } = "";
        public DateTime Date { get; set; }
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "🎉";
        public string Style { get; set; } = "FunDay";
    }

    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public string DayNumber { get; set; } = "";
        public bool IsCurrentMonth { get; set; }
        public string HolidayIcon { get; set; } = "";
        public string DayStyle { get; set; } = "NormalDay";
        public string Tooltip { get; set; } = "";
        public string AdditionalInfo { get; set; } = "";
        public string TextColor { get; set; } = "#E0E0E0";
    }
}