using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Black_Hole_Cross.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Black_Hole_Cross
{
    public partial class AboutDeveloperWindow : Window
    {
        public AboutDeveloperWindow()
        {
            InitializeComponent();
            this.DataContext = LocalizationService.Instance;
        }

        private void TelegramBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://t.me/M0f1kssbyeyoutube",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Не удалось открыть ссылку: {ex.Message}");
            }
        }

        private void YouTubeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://youtube.com/@mofikss?si=YP_8g2Wyadd0bVAI",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Не удалось открыть ссылку: {ex.Message}");
            }
        }

        private void TwitchBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.twitch.tv/m0f1kss",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Не удалось открыть ссылку: {ex.Message}");
            }
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.donationalerts.com/r/m0f1kss",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Не удалось открыть ссылку: {ex.Message}");
            }

            ShowThankYouAnimation();
        }

        private async void ShowThankYouAnimation()
        {
            var donateButton = this.FindControl<Button>("DonateButton");
            var thankYouAnimation = this.FindControl<Border>("ThankYouAnimation");

            if (donateButton == null || thankYouAnimation == null) return;

            // Скрываем кнопку доната
            donateButton.IsVisible = false;

            // Показываем анимацию
            thankYouAnimation.IsVisible = true;
            thankYouAnimation.Opacity = 0;

            // Анимация появления через таймер
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            double opacity = 0;

            timer.Tick += (s, e) =>
            {
                opacity += 0.02;
                thankYouAnimation.Opacity = Math.Min(opacity, 1);
                if (opacity >= 1)
                {
                    timer.Stop();
                }
            };
            timer.Start();

            // Ждем 3 секунды
            await Task.Delay(3000);

            // Анимация исчезновения через таймер
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            double hideOpacity = 1;

            hideTimer.Tick += (s, e) =>
            {
                hideOpacity -= 0.02;
                thankYouAnimation.Opacity = Math.Max(hideOpacity, 0);
                if (hideOpacity <= 0)
                {
                    hideTimer.Stop();
                    thankYouAnimation.IsVisible = false;
                    donateButton.IsVisible = true;
                }
            };
            hideTimer.Start();
        }

        private void ShowErrorDialog(string message)
        {
           
            var dialog = new Window
            {
                Title = "Ошибка",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 15)
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Width = 80
                        }
                    }
                }
            };

           
            var okButton = (dialog.Content as StackPanel)?.Children[1] as Button;
            if (okButton != null)
            {
                okButton.Click += (s, e) => dialog.Close();
            }

            dialog.ShowDialog(this);
        }
    }
}