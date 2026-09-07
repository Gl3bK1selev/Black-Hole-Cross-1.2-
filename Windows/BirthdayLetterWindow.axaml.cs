using Black_Hole_Cross.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Black_Hole_Cross
{
    public partial class BirthdayLetterWindow : Window
    {
        private Random _random = new Random();
        private List<string> _wishes = new List<string>
        {
            LocalizationService.Instance["BirthdayWishs1"],
            LocalizationService.Instance["BirthdayWishs2"],
            LocalizationService.Instance["BirthdayWishs3"],
            LocalizationService.Instance["BirthdayWishs4"],
            LocalizationService.Instance["BirthdayWishs5"],
            LocalizationService.Instance["BirthdayWish6"],
            LocalizationService.Instance["BirthdayWish7"],
            LocalizationService.Instance["BirthdayWish8"],
            LocalizationService.Instance["BirthdayWish9"],
            LocalizationService.Instance["BirthdayWish10"],
            LocalizationService.Instance["BirthdayWish11"],
            LocalizationService.Instance["BirthdayWish12"],
            LocalizationService.Instance["BirthdayWish13"],
            LocalizationService.Instance["BirthdayWish14"],
            LocalizationService.Instance["BirthdayWish15"],
            LocalizationService.Instance["BirthdayWish16"],
            LocalizationService.Instance["BirthdayWish17"],
            LocalizationService.Instance["BirthdayWish18"],
            LocalizationService.Instance["BirthdayWish19"],
            LocalizationService.Instance["BirthdayWish20"]
        };

        private List<DispatcherTimer> _timers = new List<DispatcherTimer>();

        public BirthdayLetterWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Устанавливаем возраст
            var birthday = SimpleSettings.UserBirthday;
            int age = DateTime.Now.Year - birthday.Year;

            var ageText = this.FindControl<TextBlock>("AgeText");
            if (ageText != null)
                ageText.Text = $"{LocalizationService.Instance["BirthdayAge"]} {age} {LocalizationService.Instance["BirthdayYears"]}! 🎉";

            // Случайное пожелание
            var wishText = this.FindControl<TextBlock>("WishText");
            if (wishText != null)
                wishText.Text = GetRandomWish();

            // Список пожеланий
            var wishesList = this.FindControl<ItemsControl>("WishesList");
            if (wishesList != null)
            {
                var selectedWishes = GetRandomWishes(3);
                wishesList.ItemsSource = selectedWishes;
            }

            // Запускаем анимации
            StartAnimations();
        }

        private string GetRandomWish()
        {
            string[] mainWishes = {
                LocalizationService.Instance["BirthdayWish1"],
                LocalizationService.Instance["BirthdayWish2"],
                LocalizationService.Instance["BirthdayWish3"],
                LocalizationService.Instance["BirthdayWish4"],
                LocalizationService.Instance["BirthdayWish5"]
            };

            return mainWishes[_random.Next(mainWishes.Length)];
        }

        private List<string> GetRandomWishes(int count)
        {
            var shuffled = new List<string>(_wishes);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[swapIndex];
                shuffled[swapIndex] = temp;
            }
            return shuffled.GetRange(0, Math.Min(count, shuffled.Count));
        }

        private void StartAnimations()
        {
            // Анимация появления письма через таймер
            var letterBorder = this.FindControl<Border>("LetterBorder");
            if (letterBorder != null)
            {
                letterBorder.Opacity = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double opacity = 0;
                double scale = 0;

                timer.Tick += (s, ev) =>
                {
                    opacity += 0.02;
                    scale += 0.02;

                    letterBorder.Opacity = Math.Min(opacity, 1);
                    letterBorder.RenderTransform = new ScaleTransform(
                        Math.Min(scale, 1),
                        Math.Min(scale, 1)
                    );

                    if (opacity >= 1 && scale >= 1)
                    {
                        timer.Stop();
                        _timers.Remove(timer);
                    }
                };
                timer.Start();
                _timers.Add(timer);
            }

            // Запускаем эффекты
            StartRandomNumbers();
            StartConfetti();
            StartFloatingBalloons();
        }

        private void StartRandomNumbers()
        {
            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            var numbersCanvas = this.FindControl<Canvas>("NumbersCanvas");

            if (mainCanvas == null || numbersCanvas == null) return;

            for (int i = 0; i < 50; i++)
            {
                CreateRandomNumber(mainCanvas, numbersCanvas);
            }
        }

        private void CreateRandomNumber(Canvas mainCanvas, Canvas numbersCanvas)
        {
            var number = new TextBlock
            {
                Text = _random.Next(1, 100).ToString(),
                FontSize = _random.Next(20, 40),
                Foreground = GetRandomTransparentColor(),
                Opacity = 0.7,
                RenderTransform = new RotateTransform(_random.Next(-45, 45))
            };

            double left = _random.Next(0, (int)(mainCanvas.Bounds.Width - 50));
            double top = _random.Next(0, (int)(mainCanvas.Bounds.Height - 30));

            Canvas.SetLeft(number, left);
            Canvas.SetTop(number, top);
            numbersCanvas.Children.Add(number);

            // Анимация мерцания через таймер
            var blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            bool visible = true;

            blinkTimer.Tick += (s, e) =>
            {
                visible = !visible;
                number.Opacity = visible ? 0.9 : 0.3;
            };
            blinkTimer.Start();
            _timers.Add(blinkTimer);

            // Анимация плавания через таймер
            var floatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            double currentTop = top;
            double direction = _random.Next(1, 3) == 1 ? 1 : -1;
            double step = _random.Next(1, 3);
            double maxOffset = _random.Next(30, 60);
            double offset = 0;

            floatTimer.Tick += (s, e) =>
            {
                offset += step * direction;
                if (Math.Abs(offset) > maxOffset) direction *= -1;
                Canvas.SetTop(number, currentTop + offset);
            };
            floatTimer.Start();
            _timers.Add(floatTimer);
        }

        private void StartConfetti()
        {
            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            var confettiCanvas = this.FindControl<Canvas>("ConfettiCanvas");

            if (mainCanvas == null || confettiCanvas == null) return;

            for (int i = 0; i < 150; i++)
            {
                CreateConfettiPiece(mainCanvas, confettiCanvas);
            }
        }

        private void CreateConfettiPiece(Canvas mainCanvas, Canvas confettiCanvas)
        {
            var confetti = new Rectangle
            {
                Width = _random.Next(6, 12),
                Height = _random.Next(6, 12),
                Fill = GetRandomColor(),
                RenderTransform = new RotateTransform()
            };

            double startX = _random.Next(0, (int)(mainCanvas.Bounds.Width));
            Canvas.SetLeft(confetti, startX);
            Canvas.SetTop(confetti, -10);
            confettiCanvas.Children.Add(confetti);

            // Анимация падения через таймер
            double currentTop = -10;
            double speed = _random.Next(1, 3);
            double sway = _random.Next(-2, 2);
            double currentSway = 0;

            var fallTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            bool isFalling = true;

            fallTimer.Tick += (s, e) =>
            {
                if (isFalling)
                {
                    currentTop += speed;
                    currentSway += sway;

                    Canvas.SetTop(confetti, currentTop);
                    Canvas.SetLeft(confetti, startX + currentSway);

                    // Вращение
                    if (confetti.RenderTransform is RotateTransform rotate)
                        rotate.Angle += 5;

                    // Проверка выхода за границы
                    if (currentTop > mainCanvas.Bounds.Height + 10)
                    {
                        isFalling = false;
                        fallTimer.Stop();
                        confettiCanvas.Children.Remove(confetti);
                        _timers.Remove(fallTimer);
                        // Создаем новое конфетти
                        CreateConfettiPiece(mainCanvas, confettiCanvas);
                    }
                }
            };
            fallTimer.Start();
            _timers.Add(fallTimer);
        }

        private void StartFloatingBalloons()
        {
            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            var balloonsCanvas = this.FindControl<Canvas>("BalloonsCanvas");

            if (mainCanvas == null || balloonsCanvas == null) return;

            var birthday = SimpleSettings.UserBirthday;
            int age = DateTime.Now.Year - birthday.Year;

            // Создаем шарики с цифрами возраста
            for (int i = 0; i < Math.Min(age, 25); i++)
            {
                CreateBalloon(mainCanvas, balloonsCanvas, (i + 1).ToString());
            }

            // Добавляем несколько обычных шариков
            for (int i = 0; i < 10; i++)
            {
                CreateBalloon(mainCanvas, balloonsCanvas, "🎈");
            }
        }

        private void CreateBalloon(Canvas mainCanvas, Canvas balloonsCanvas, string content)
        {
            var balloon = new Border
            {
                Background = GetRandomPastelColor(),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(12, 8, 12, 8),
                RenderTransform = new TransformGroup(),
                Child = new TextBlock
                {
                    Text = content,
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            };

            double startX = _random.Next(0, (int)(mainCanvas.Bounds.Width - 60));
            double startY = mainCanvas.Bounds.Height + 50;

            Canvas.SetLeft(balloon, startX);
            Canvas.SetTop(balloon, startY);
            balloonsCanvas.Children.Add(balloon);

            // Анимация взлета
            double currentTop = startY;
            double currentX = startX;
            double sway = _random.Next(1, 3) * (_random.Next(0, 2) == 0 ? 1 : -1);

            var riseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            double step = _random.Next(1, 3);
            double pulseStep = 0;
            bool pulseDirection = true;

            riseTimer.Tick += (s, e) =>
            {
                currentTop -= step;
                currentX += sway * 0.5;

                // Пульсация
                if (pulseDirection)
                {
                    pulseStep += 0.02;
                    if (pulseStep >= 0.2) pulseDirection = false;
                }
                else
                {
                    pulseStep -= 0.02;
                    if (pulseStep <= -0.2) pulseDirection = true;
                }

                Canvas.SetTop(balloon, currentTop);
                Canvas.SetLeft(balloon, currentX);

                if (balloon.RenderTransform is TransformGroup group)
                {
                    // Находим или создаем ScaleTransform
                    var scaleTransform = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                    if (scaleTransform != null)
                    {
                        scaleTransform.ScaleX = 1 + pulseStep;
                        scaleTransform.ScaleY = 1 + pulseStep;
                    }
                    else
                    {
                        group.Children.Add(new ScaleTransform(1 + pulseStep, 1 + pulseStep));
                    }
                }

                // Проверка выхода за границы
                if (currentTop < -100)
                {
                    riseTimer.Stop();
                    balloonsCanvas.Children.Remove(balloon);
                    _timers.Remove(riseTimer);
                }
            };
            riseTimer.Start();
            _timers.Add(riseTimer);
        }

        private IBrush GetRandomColor()
        {
            Color[] colors = {
                Colors.Gold, Colors.LightBlue, Colors.LightGreen,
                Colors.LightPink, Colors.Violet, Colors.Orange,
                Colors.Cyan, Colors.Magenta, Colors.Yellow, Colors.LightCoral
            };

            return new SolidColorBrush(colors[_random.Next(colors.Length)]);
        }

        private IBrush GetRandomPastelColor()
        {
            Color[] pastelColors = {
                Color.FromArgb(255, 255, 182, 193), // LightPink
                Color.FromArgb(255, 173, 216, 230), // LightBlue
                Color.FromArgb(255, 152, 251, 152), // PaleGreen
                Color.FromArgb(255, 255, 218, 185), // PeachPuff
                Color.FromArgb(255, 221, 160, 221), // Plum
                Color.FromArgb(255, 240, 230, 140), // Khaki
                Color.FromArgb(255, 175, 238, 238), // PaleTurquoise
                Color.FromArgb(255, 255, 192, 203)  // Pink
            };

            return new SolidColorBrush(pastelColors[_random.Next(pastelColors.Length)]);
        }

        private IBrush GetRandomTransparentColor()
        {
            Color[] colors = {
                Colors.Gold, Colors.LightBlue, Colors.LightGreen,
                Colors.White, Colors.LightYellow, Colors.LightCyan
            };

            var color = colors[_random.Next(colors.Length)];
            return new SolidColorBrush(Color.FromArgb(128, color.R, color.G, color.B));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Останавливаем все таймеры
            foreach (var timer in _timers)
            {
                try { timer.Stop(); } catch { }
            }
            _timers.Clear();

            // Анимация закрытия через таймер
            var letterBorder = this.FindControl<Border>("LetterBorder");
            if (letterBorder != null)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double scale = 1;
                double opacity = 1;

                timer.Tick += (s, ev) =>
                {
                    scale -= 0.02;
                    opacity -= 0.02;

                    letterBorder.RenderTransform = new ScaleTransform(Math.Max(scale, 0), Math.Max(scale, 0));
                    letterBorder.Opacity = Math.Max(opacity, 0);

                    if (scale <= 0 || opacity <= 0)
                    {
                        timer.Stop();
                        this.Close();
                    }
                };
                timer.Start();
            }
            else
            {
                this.Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // Останавливаем все таймеры при закрытии
            foreach (var timer in _timers)
            {
                try { timer.Stop(); } catch { }
            }
            _timers.Clear();
        }
    }
}