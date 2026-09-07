using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Black_Hole_Cross.Views;
using BlackHole;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Black_Hole_Cross
{
    public partial class SplashScreen : Window
    {
        public SplashScreen(string userName = null)
        {
            InitializeComponent();

            // Центрируем окно
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            string displayName = userName ?? SimpleSettings.UserName ?? "Пользователь";
            StartAnimationSequence(displayName);
        }

        private async void StartAnimationSequence(string userName)
        {
            try
            {
                // 1. Показываем маленькую черную дыру (круг)
                await ShowBlackHoleAnimation();

                // 2. Увеличиваем окно
                await ExpandWindowAnimation();

                // 3. Запускаем вращение
                StartRotationAnimation();

                // 4. Показываем надпись BLACK HOLE
                await ShowTitleAnimation();

                // 5. Показываем приветствие
                await ShowWelcomeAnimation(userName);

                // 6. Ждем 2 секунды
                await Task.Delay(2000);

                // 7. Переходим к главному окну
                TransitionToMainWindow();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка в анимации сплеш-скрина: {ex.Message}");
                TransitionToMainWindow();
            }
        }

        private async Task ShowBlackHoleAnimation()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double opacity = 0;

                timer.Tick += (s, e) =>
                {
                    opacity += 0.02;

                    if (BlackHole != null)
                        BlackHole.Opacity = Math.Min(opacity, 1);

                    if (opacity >= 1)
                        timer.Stop();
                };

                timer.Start();
            });

            await Task.Delay(1500);
        }

        private async Task ExpandWindowAnimation()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double currentWidth = this.Width;
                double currentHeight = this.Height;
                double targetWidth = 600;
                double targetHeight = 500;
                double step = 0;

                timer.Tick += (s, e) =>
                {
                    step += 0.02;
                    double progress = Math.Min(step, 1);

                    // Плавное увеличение
                    double newWidth = currentWidth + (targetWidth - currentWidth) * progress;
                    double newHeight = currentHeight + (targetHeight - currentHeight) * progress;

                    this.Width = newWidth;
                    this.Height = newHeight;

                    if (progress >= 1)
                        timer.Stop();
                };

                timer.Start();
            });

            await Task.Delay(1500);
        }

        private void StartRotationAnimation()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
             
                if (BlackHole != null)
                {
                    var blackHoleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                    double blackHoleAngle = 0;

                    blackHoleTimer.Tick += (s, e) =>
                    {
                        blackHoleAngle += 0.5;
                        
                        BlackHole.RenderTransform = new RotateTransform(blackHoleAngle % 360);
                    };
                    blackHoleTimer.Start();
                }

               
                if (AccretionDisk != null)
                {
                    var diskTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                    double diskAngle = 0;

                    diskTimer.Tick += (s, e) =>
                    {
                        diskAngle -= 0.7;
                        AccretionDisk.RenderTransform = new RotateTransform(diskAngle % 360);
                    };
                    diskTimer.Start();

                   
                    var opacityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                    double opacity = 0;

                    opacityTimer.Tick += (s, e) =>
                    {
                        opacity += 0.015;
                        AccretionDisk.Opacity = Math.Min(opacity, 0.7);
                        if (opacity >= 0.7)
                            opacityTimer.Stop();
                    };
                    opacityTimer.Start();
                }
            });
        }

        private async Task ShowTitleAnimation()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (TitleText == null) return;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double opacity = 0;
                double fontSize = 10;
                double targetFontSize = 24;

                timer.Tick += (s, e) =>
                {
                    opacity += 0.015;
                    fontSize += (targetFontSize - 10) * 0.015;

                    TitleText.Opacity = Math.Min(opacity, 1);
                    TitleText.FontSize = Math.Min(fontSize, targetFontSize);

                    if (opacity >= 1 && fontSize >= targetFontSize)
                        timer.Stop();
                };

                timer.Start();
            });

            await Task.Delay(2000);
        }

        private async Task ShowWelcomeAnimation(string userName)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (WelcomeText == null) return;

                // Устанавливаем текст приветствия
                WelcomeText.Text = $"Добро пожаловать, {userName}!";

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double opacity = 0;
                double fontSize = 8;
                double targetFontSize = 16;

                timer.Tick += (s, e) =>
                {
                    opacity += 0.015;
                    fontSize += (targetFontSize - 8) * 0.015;

                    WelcomeText.Opacity = Math.Min(opacity, 1);
                    WelcomeText.FontSize = Math.Min(fontSize, targetFontSize);

                    if (opacity >= 1 && fontSize >= targetFontSize)
                        timer.Stop();
                };

                timer.Start();
            });

            await Task.Delay(2000);
        }

        private void TransitionToMainWindow()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // Анимация исчезновения
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                double opacity = 1;

                timer.Tick += (s, e) =>
                {
                    opacity -= 0.02;
                    this.Opacity = Math.Max(opacity, 0);

                    if (opacity <= 0)
                    {
                        timer.Stop();

                      
                        this.Close();

                      
                        var mainWindow = new MainWindow();

                        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            desktop.MainWindow = mainWindow;
                        }

                        mainWindow.Show();
                    }
                };

                timer.Start();
            });
        }
    }
}