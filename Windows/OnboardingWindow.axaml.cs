using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using BlackHole;
using System.Collections.Generic;
using System.Diagnostics;

namespace Black_Hole_Cross
{
    public partial class OnboardingWindow : Window
    {
        private int _currentSlide;
        private readonly List<Control> _slides = new();
        private TextBox? _nameTextBox;

        private readonly IBrush _activeDotBrush = new SolidColorBrush(Color.Parse("#00E5FF"));
        private readonly IBrush _inactiveDotBrush = new SolidColorBrush(Color.Parse("#27273A"));

        public OnboardingWindow()
        {
            InitializeComponent();
            CreateSlides();
            ShowSlide(0);
        }

        private void CreateSlides()
        {
            _slides.Add(CreateSlide(
                "🌌 ДОБРО ПОЖАЛОВАТЬ В BLACK HOLE",
                "Мощный кроссплатформенный комплекс для диагностики и оптимизации системы.\nМаксимальная скорость, умный контроль и ничего лишнего."));

            _slides.Add(CreateSlide(
                "🛡️ ПОЛНАЯ БЕЗОПАСНОСТЬ И ПРИВАТНОСТЬ",
                "• Все конфиги хранятся локально на твоём устройстве\n• Нулевой трекинг, отсутствие скрытого телеметрии\n• Прозрачная работа сетевых адаптеров и сервисов"));

            _slides.Add(CreateSlide(
                "⚡ ВОЗМОЖНОСТИ И ТВИКИ",
                "🌐 Оптимизация сети, маршрутов и пинга в один клик\n💻 Глубокий мониторинг нагрузки ЦП, памяти и адаптеров\n🚀 Твики операционной системы и автонастройка параметров"));

            _slides.Add(CreateNameInputSlide());
        }

        private static StackPanel CreateSlide(string title, string description)
        {
            return new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 26,
                        FontWeight = FontWeight.Black,
                        Foreground = Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        LetterSpacing = 1
                    },
                    new TextBlock
                    {
                        Text = description,
                        FontSize = 15,
                        Foreground = new SolidColorBrush(Color.Parse("#A1A1AA")),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        MaxWidth = 650
                    }
                }
            };
        }

        private Control CreateNameInputSlide()
        {
            _nameTextBox = new TextBox
            {
                Width = 320,
                Height = 46,
                FontSize = 15,
                Watermark = "Введи имя...",
                Background = new SolidColorBrush(Color.Parse("#121218")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#2A2A36")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CaretBrush = new SolidColorBrush(Color.Parse("#00E5FF"))
            };

            _nameTextBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_nameTextBox.Text))
                    CompleteOnboarding();
            };

            _nameTextBox.TextChanged += (_, _) =>
            {
                NextButton.Content = string.IsNullOrEmpty(_nameTextBox.Text) ? "ПРОПУСТИТЬ ➡" : "ГОТОВО ✅";
            };

            return new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "👋 КАК К ТЕБЕ ОБРАЩАТЬСЯ?",
                        FontSize = 24,
                        FontWeight = FontWeight.Black,
                        Foreground = Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10)
                    },
                    _nameTextBox,
                    new TextBlock
                    {
                        Text = "Имя используется для персонализации статусов и отчетов диагностики",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.Parse("#71717A")),
                        TextAlignment = TextAlignment.Center
                    }
                }
            };
        }

        private void ShowSlide(int slideIndex)
        {
            _currentSlide = slideIndex;
            SlideContainer.Children.Clear();
            SlideContainer.Children.Add(_slides[slideIndex]);
            UpdateProgressDots();

            BackButton.IsVisible = slideIndex > 0;

            if (slideIndex == 3)
            {
                NextButton.Content = string.IsNullOrEmpty(_nameTextBox?.Text) ? "ПРОПУСТИТЬ ➡" : "ГОТОВО ✅";
            }
            else
            {
                NextButton.Content = "ДАЛЕЕ ▶";
            }
        }

        private void UpdateProgressDots()
        {
            Dot1.Fill = _currentSlide >= 0 ? _activeDotBrush : _inactiveDotBrush;
            Dot2.Fill = _currentSlide >= 1 ? _activeDotBrush : _inactiveDotBrush;
            Dot3.Fill = _currentSlide >= 2 ? _activeDotBrush : _inactiveDotBrush;
            Dot4.Fill = _currentSlide >= 3 ? _activeDotBrush : _inactiveDotBrush;
        }

        private void NextButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_currentSlide < _slides.Count - 1)
                ShowSlide(_currentSlide + 1);
            else
                CompleteOnboarding();
        }

        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_currentSlide > 0)
                ShowSlide(_currentSlide - 1);
        }

        private void CompleteOnboarding()
        {
            SimpleSettings.UserName = _nameTextBox != null && !string.IsNullOrWhiteSpace(_nameTextBox.Text)
                ? _nameTextBox.Text.Trim()
                : "Пользователь";

            SimpleSettings.OnboardingCompleted = true;
            SimpleSettings.SaveSettings();

           
            var menu = new ManagerMenu();
            menu.Show();

            
            Close();
        }
    }
}
