using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Black_Hole_Cross;

public partial class NotificationWindow : Window
{
    private const int DisplayTimeMs = 5000;
    private const int AnimationFps = 60;

    private Stopwatch _stopwatch = new();
    private DispatcherTimer? _renderTimer;
    private bool _isClosing = false;
    private int _targetY;
    private int _startY;

    public NotificationWindow()
    {
        InitializeComponent();
    }

    private void PositionWindow()
    {
        var screens = Screens.All;
        if (screens.Count > 0)
        {
            var workArea = screens[0].WorkingArea;
            int rightX = workArea.X + workArea.Width - (int)Width - 15;

            _targetY = workArea.Y + workArea.Height - (int)Height - 15;
            _startY = _targetY + 25;

            Position = new PixelPoint(rightX, _startY);
        }
    }

    public async void ShowNotification(SystemNotification notification)
    {
        TitleText.Text = notification.Title;
        MessageText.Text = notification.Message;
        IconText.Text = notification.Icon;
        SetNotificationStyle(notification.Type);

        PositionWindow();
        Show();
        Topmost = true;

       
        await AnimateInAsync();

      
        _stopwatch.Restart();
        StartRenderTimer();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Focus();
    }

    private void SetNotificationStyle(NotificationType type)
    {
        Color primaryColor = type switch
        {
            NotificationType.Warning => Color.Parse("#FF9800"),
            NotificationType.Error => Color.Parse("#F44336"),
            NotificationType.Critical => Color.Parse("#E91E63"),
            NotificationType.Success => Color.Parse("#4CAF50"),
            _ => Color.Parse("#5C6BC0")
        };

        var brush = new SolidColorBrush(primaryColor);
        NotificationBorder.BorderBrush = brush;
        TimerProgressBar.Foreground = brush;
        IconBadge.Background = new SolidColorBrush(Color.FromArgb(40, primaryColor.R, primaryColor.G, primaryColor.B));
        NotificationBorder.BoxShadow = BoxShadows.Parse($"0 6 20 0 #{primaryColor.A:X2}{primaryColor.R:X2}{primaryColor.G:X2}{primaryColor.B:X2}");
    }

    private void StartRenderTimer()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / AnimationFps) };
        _renderTimer.Tick += (_, _) =>
        {
            double elapsed = _stopwatch.ElapsedMilliseconds;
            double remainingPercent = 100.0 - (elapsed / DisplayTimeMs * 100.0);

            if (remainingPercent <= 0)
            {
                TimerProgressBar.Value = 0;
                _renderTimer.Stop();
                CloseWithAnimation();
            }
            else
            {
                TimerProgressBar.Value = remainingPercent;
            }
        };
        _renderTimer.Start();
    }

    #region Animations (Fade In / Fade Out)
    private async Task AnimateInAsync()
    {
        int steps = 15;
        for (int i = 1; i <= steps; i++)
        {
            double progress = (double)i / steps;
            Opacity = progress;

            int currentY = (int)(_startY - (_startY - _targetY) * Math.Sin(progress * Math.PI / 2));
            Position = new PixelPoint(Position.X, currentY);

            await Task.Delay(10);
        }
        Opacity = 1;
        Position = new PixelPoint(Position.X, _targetY);
    }

    private async void CloseWithAnimation()
    {
        if (_isClosing) return;
        _isClosing = true;

        _renderTimer?.Stop();
        _stopwatch.Stop();

        int steps = 12;
        int initialY = Position.Y;
        for (int i = steps; i >= 0; i--)
        {
            double progress = (double)i / steps;
            Opacity = progress;
            Position = new PixelPoint(Position.X, initialY + (int)((1 - progress) * 15));
            await Task.Delay(10);
        }

        Close();
    }
    #endregion

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    protected override void OnPointerEntered(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _stopwatch.Stop();
        _renderTimer?.Stop();
        TimerProgressBar.IsVisible = false;
    }

    protected override void OnPointerExited(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_isClosing) return;

        TimerProgressBar.IsVisible = true;
        _stopwatch.Start();
        _renderTimer?.Start();
    }
}
