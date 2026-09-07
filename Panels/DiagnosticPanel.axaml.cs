using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Black_Hole_Cross.ControlDiagnostic;
using Path = Avalonia.Controls.Shapes.Path;

namespace Black_Hole_Cross.Panels
{
    public partial class DiagnosticPanel : UserControl
    {
        private static readonly Random _rnd = new Random();
        private readonly DispatcherTimer _effectsTimer = new DispatcherTimer(DispatcherPriority.Render);
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly List<Particle> _particles = new List<Particle>();
        private TimeSpan _lastTime;

        private enum SummerWeather { FirefliesWithMeteors, CloudMist, SolarDust, SmoothRain }
        private SummerWeather _currentSummerWeather;

        private readonly Dictionary<string, string[]> _greetings = new()
        {
            ["Morning"] = new[] { "Солнце встаёт, система просыпается...", "Новый день, новые вычисления!", "Кофе готов? Система — да!", "Утро — время оптимизации!" },
            ["Day"] = new[] { "Работа кипит, процессы бегут!", "Ищем неисправности в белом свете дня!", "Пиковая производительность!", "День в разгаре — система на максимуме!" },
            ["Evening"] = new[] { "Закат... время подвести итоги дня!", "Вечерние патрули памяти...", "Сумерки — лучший тест для подсветки!", "Готовим систему к ночному режиму!" },
            ["Night"] = new[] { "Тишина... только фоновые процессы...", "Не спишь? Система бдит с тобой!", "Ночью все баги выходят на охоту!", "Звёзды горят, сервера шумят..." }
        };

        public DiagnosticPanel()
        {
            InitializeComponent();
            SetRandomGreeting();
            SimpleSettings.SettingsChanged += () => UpdateSeasonalAnimation();
            _currentSummerWeather = (SummerWeather)_rnd.Next(0, 4);

            Loaded += async (_, _) =>
            {
                await AnimateFadeInAsync();
                StartSeasonalEffects();
                if (SimpleSettings.EnableSeasonalAnimations)
                {
                    StartSeasonalEffects();
                }
                else
                {
                    EffectsCanvas.IsVisible = false;
                    _effectsTimer?.Stop();
                }
                
                
            };

            Unloaded += (_, _) =>
            {
                _effectsTimer.Stop();
                _stopwatch.Stop();
            };
        }

      
        private void UpdateSeasonalAnimation()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (EffectsCanvas != null)
                {
                    EffectsCanvas.IsVisible = SimpleSettings.EnableSeasonalAnimations;
                }
                if(_effectsTimer != null)
                {
                    if (SimpleSettings.EnableSeasonalAnimations)
                    {
                        if (!_effectsTimer.IsEnabled)
                        {
                            _effectsTimer.Start();
                        }
                        
                    }
                    else
                    {
                        _effectsTimer.Stop();
                        EffectsCanvas?.Children.Clear();
                    }
                }
            });
           
        }

        protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            SimpleSettings.SettingsChanged -= UpdateSeasonalAnimation;
        }
        private void CpuButton_Click(object? sender, RoutedEventArgs e)
        {
           
            SetContentAndActiveButton(sender as Button, new CpuControl());
        }

        private void RAMButton_Click(object? sender, RoutedEventArgs e)
        {
           
            SetContentAndActiveButton(sender as Button, new RamControl());
        }

        private void DiskButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new SsdControl());
        }

        private void GPUButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new GpuControl());
        }

        private void MBButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new MBcontrol());
        }

        private void KeyboardButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new KeyboardControl());
        }

        private void MouseButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new MouseControl());
        }

        private void FANButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new AudioControl());
        }
        private void NetworkButton_Click(object? sender, RoutedEventArgs e)
        {
            SetContentAndActiveButton(sender as Button, new NetworkControl());
        }

    
        private void SetContentAndActiveButton(Button? targetButton, Control viewContent)
        {
            if (targetButton != null && TabButtonsPanel != null)
            {
                foreach (var child in TabButtonsPanel.Children)
                {
                    if (child is Button btn)
                        btn.Classes.Remove("active");
                }
                targetButton.Classes.Add("active");
            }

            if (TabContentArea != null)
            {
                TabContentArea.Content = viewContent;
            }
        }

       
        private void SetRandomGreeting()
        {
            var hour = DateTime.Now.Hour;
            string category = hour switch
            {
                >= 5 and < 12 => "Morning",
                >= 12 and < 17 => "Day",
                >= 17 and < 22 => "Evening",
                _ => "Night"
            };

            var pool = _greetings[category];
            if (GreetingText != null)
                GreetingText.Text = pool[_rnd.Next(pool.Length)];
        }

        private async Task AnimateFadeInAsync()
        {
            if (GreetingText == null) return;
            for (double opacity = 0; opacity <= 1.0; opacity += 0.05)
            {
                GreetingText.Opacity = opacity;
                await Task.Delay(15);
            }
            GreetingText.Opacity = 1.0;
        }

        private void StartSeasonalEffects()
        {
            _stopwatch.Start();
            _lastTime = _stopwatch.Elapsed;
            _effectsTimer.Interval = TimeSpan.FromMilliseconds(16);
            _effectsTimer.Tick += UpdateParticles;
            _effectsTimer.Start();

            int month = DateTime.Now.Month;
            int count = (month == 12 || month <= 2) ? 65 : 45;

            for (int i = 0; i < count; i++)
            {
                SpawnParticle(month, isInitial: true);
            }
        }

        private void SpawnParticle(int month, bool isInitial = false)
        {
            if (EffectsCanvas == null) return;

            Shape shape;
            double speedY = 1.0;
            double speedX = 0;
            double rotationSpeed = 0;

            if (month is 9 or 10 or 11)
            {
                var colors = new[] { "#E65100", "#D84315", "#F57C00", "#FFB300" };
                int size = _rnd.Next(16, 24);
                shape = new Path
                {
                    Data = Geometry.Parse("M 0,8 C 4,0 12,0 16,8 C 12,16 4,16 0,8 Z"),
                    Fill = SolidColorBrush.Parse(colors[_rnd.Next(colors.Length)]),
                    Width = size,
                    Height = size,
                    Opacity = 0.8,
                    RenderTransform = new RotateTransform(0)
                };
                speedY = _rnd.NextDouble() * 1.2 + 0.8;
                speedX = (_rnd.NextDouble() - 0.5) * 1.2;
                rotationSpeed = (_rnd.NextDouble() - 0.5) * 2.5;
            }
            else if (month is 12 or 1 or 2)
            {
                int size = _rnd.Next(4, 10);
                shape = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Opacity = _rnd.NextDouble() * 0.7 + 0.3
                };
                speedY = _rnd.NextDouble() * 3.5 + 2.5;
                speedX = (_rnd.NextDouble() - 0.5) * 1.0;
            }
            else if (month is 3 or 4 or 5)
            {
                var colors = new[] { "#FFB7C5", "#FFC0CB", "#F8BBD0", "#FFFFFF" };
                int size = _rnd.Next(10, 16);
                shape = new Ellipse
                {
                    Width = size,
                    Height = size * 1.3,
                    Fill = SolidColorBrush.Parse(colors[_rnd.Next(colors.Length)]),
                    Opacity = 0.8,
                    RenderTransform = new RotateTransform(_rnd.Next(0, 360))
                };
                speedY = _rnd.NextDouble() * 1.2 + 0.8;
                speedX = _rnd.NextDouble() * 2.0 + 0.6;
                rotationSpeed = _rnd.NextDouble() * 1.5;
            }
            else
            {
                switch (_currentSummerWeather)
                {
                    case SummerWeather.SmoothRain:
                        shape = new Rectangle
                        {
                            Width = 1.6,
                            Height = _rnd.Next(18, 28),
                            Fill = SolidColorBrush.Parse("#8090CAF9"),
                            Opacity = 0.6
                        };
                        speedY = _rnd.NextDouble() * 6 + 8;
                        speedX = -0.5;
                        break;

                    case SummerWeather.FirefliesWithMeteors:
                        bool isMeteor = _rnd.Next(0, 10) > 7;
                        if (isMeteor)
                        {
                            shape = new Rectangle
                            {
                                Width = 2.5,
                                Height = 30,
                                Fill = SolidColorBrush.Parse("#FFFFD54F"),
                                Opacity = 0.95,
                                RenderTransform = new RotateTransform(45)
                            };
                            speedY = 10;
                            speedX = 10;
                        }
                        else
                        {
                            shape = new Ellipse
                            {
                                Width = 6,
                                Height = 6,
                                Fill = SolidColorBrush.Parse("#FFF59D"),
                                Opacity = _rnd.NextDouble() * 0.8 + 0.2
                            };
                            speedY = (_rnd.NextDouble() - 0.5) * 0.3;
                            speedX = (_rnd.NextDouble() - 0.5) * 0.3;
                        }
                        break;

                    case SummerWeather.SolarDust:
                        shape = new Ellipse
                        {
                            Width = _rnd.Next(6, 12),
                            Height = _rnd.Next(6, 12),
                            Fill = SolidColorBrush.Parse("#40FFE082"),
                            Opacity = 0.5
                        };
                        speedY = -(_rnd.NextDouble() * 0.5 + 0.2);
                        speedX = (_rnd.NextDouble() - 0.5) * 0.6;
                        break;

                    default:
                        int cloudSize = _rnd.Next(50, 90);
                        shape = new Ellipse
                        {
                            Width = cloudSize,
                            Height = cloudSize / 2,
                            Fill = SolidColorBrush.Parse("#10FFFFFF"),
                            Opacity = 0.2
                        };
                        speedY = 0.05;
                        speedX = _rnd.NextDouble() * 0.4 + 0.2;
                        break;
                }
            }

            double width = Bounds.Width > 0 ? Bounds.Width : 1000;
            double height = Bounds.Height > 0 ? Bounds.Height : 650;

            double posX = _rnd.NextDouble() * width;
            double posY = isInitial ? _rnd.NextDouble() * height : -40;

            Canvas.SetLeft(shape, posX);
            Canvas.SetTop(shape, posY);

            EffectsCanvas.Children.Add(shape);
            _particles.Add(new Particle(shape, posX, posY, speedX, speedY, rotationSpeed));
        }

        private void UpdateParticles(object? sender, EventArgs e)
        {
            if (EffectsCanvas == null) return;

            TimeSpan currentTime = _stopwatch.Elapsed;
            double deltaTime = (currentTime - _lastTime).TotalSeconds;
            _lastTime = currentTime;

            double factor = deltaTime * 60.0;
            if (factor > 2.0 || factor < 0.2) factor = 1.0;

            double width = Bounds.Width > 0 ? Bounds.Width : 1000;
            double height = Bounds.Height > 0 ? Bounds.Height : 650;

            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.Y += p.SpeedY * factor;
                p.X += p.SpeedX * factor;

                if (p.RotationSpeed != 0 && p.Shape.RenderTransform is RotateTransform rt)
                {
                    rt.Angle += p.RotationSpeed * factor;
                }

                if (p.Y > height + 40 || p.Y < -50 || p.X > width + 50 || p.X < -50)
                {
                    p.Y = -30;
                    p.X = _rnd.NextDouble() * width;
                }

                Canvas.SetTop(p.Shape, p.Y);
                Canvas.SetLeft(p.Shape, p.X);
            }
        }

        private class Particle
        {
            public Shape Shape { get; }
            public double X { get; set; }
            public double Y { get; set; }
            public double SpeedX { get; }
            public double SpeedY { get; }
            public double RotationSpeed { get; }

            public Particle(Shape shape, double x, double y, double speedX, double speedY, double rotationSpeed = 0)
            {
                Shape = shape; X = x; Y = y; SpeedX = speedX; SpeedY = speedY; RotationSpeed = rotationSpeed;
            }
        }
    }
}
