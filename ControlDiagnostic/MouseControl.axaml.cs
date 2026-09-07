using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Black_Hole_Cross.Services;
using System;
using System.Runtime.InteropServices;

namespace Black_Hole_Cross.ControlDiagnostic
{
    public partial class MouseControl : UserControl
    {
        private int _clickCount = 0;
        private double _totalScrollDelta = 0;

        public MouseControl()
        {
            InitializeComponent();

            Loaded += MouseControl_Loaded;

           
            DiagnosMouse.Text = LocalizationService.Instance["MouseDiagnostics"] ?? "Диагностика манипулятора (Мышь)";
            descrip.Text = LocalizationService.Instance["MouseDesc"] ?? "Проверка кликов, колеса прокрутки, координат и отклика";
        }

        private void MouseControl_Loaded(object? sender, RoutedEventArgs e)
        {
            ShowMouseStaticInfo();

         
            TestArea.PointerPressed += TestArea_PointerPressed;
            TestArea.PointerReleased += TestArea_PointerReleased;
            TestArea.PointerMoved += TestArea_PointerMoved;
            TestArea.PointerWheelChanged += TestArea_PointerWheelChanged;
        }

      
        private void TestArea_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(TestArea);
            _clickCount++;
            ClickCountText.Text = $"Всего кликов: {_clickCount}";

            if (point.Properties.IsLeftButtonPressed)
            {
                SetButtonHighlight(BtnLeft, true);
                LastActionText.Text = "Нажата: Левая кнопка (LMB)";
            }
            if (point.Properties.IsRightButtonPressed)
            {
                SetButtonHighlight(BtnRight, true);
                LastActionText.Text = "Нажата: Правая кнопка (RMB)";
            }
            if (point.Properties.IsMiddleButtonPressed)
            {
                SetButtonHighlight(BtnMiddle, true);
                LastActionText.Text = "Нажато: Колёсико (MMB)";
            }
            if (point.Properties.IsXButton1Pressed)
            {
                SetButtonHighlight(BtnBack, true);
                LastActionText.Text = "Нажата: Боковая назад (XButton1)";
            }
            if (point.Properties.IsXButton2Pressed)
            {
                SetButtonHighlight(BtnForward, true);
                LastActionText.Text = "Нажата: Боковая вперёд (XButton2)";
            }
        }

        private void TestArea_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
           
            SetButtonVisitedAll();
        }

       
        private void TestArea_PointerMoved(object? sender, PointerEventArgs e)
        {
            var pos = e.GetPosition(TestArea);
            CoordsText.Text = $"X: {pos.X:F0} px | Y: {pos.Y:F0} px";
        }

      
        private void TestArea_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            _totalScrollDelta += e.Delta.Y;
            string direction = e.Delta.Y > 0 ? "Вверх ⬆️" : "Вниз ⬇️";
            ScrollText.Text = $"Скролл: {direction} (Дельта: {_totalScrollDelta:F0})";
            SetButtonHighlight(BtnMiddle, true);
        }

        private void SetButtonHighlight(Border btn, bool active)
        {
            if (btn != null)
            {
                btn.Background = active
                    ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) // Синий при нажатии
                    : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Зеленый (работает)
            }
        }

        private void SetButtonVisitedAll()
        {
           
            BtnLeft.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            BtnRight.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            BtnMiddle.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            BtnBack.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            BtnForward.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
        }

        private void ResetTest_Click(object? sender, RoutedEventArgs e)
        {
            _clickCount = 0;
            _totalScrollDelta = 0;
            ClickCountText.Text = "Всего кликов: 0";
            ScrollText.Text = "Скролл: Ожидание...";
            LastActionText.Text = "Кликните в зоне тестирования";

            var defaultBrush = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            BtnLeft.Background = defaultBrush;
            BtnRight.Background = defaultBrush;
            BtnMiddle.Background = defaultBrush;
            BtnBack.Background = defaultBrush;
            BtnForward.Background = defaultBrush;
        }

        private void ShowMouseStaticInfo()
        {
            string osDriver = "Standard Pointer System";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) osDriver = "Windows User32 Input / Raw Input API";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) osDriver = "Linux Evdev Pointer Subsystem";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) osDriver = "macOS CoreGraphics Pointer Manager";

            InfoText.Text = $"🖱️ Устройство: Оптический / Лазерный манипулятор (USB/Bluetooth/Trackpad)\n" +
                            $"🔌 Драйвер ввода: {osDriver}\n" +
                            $"🎯 Поддержка дополнительных кнопок: 5+ Кнопок (LMB, RMB, MMB, X1, X2)\n" +
                            $"🔄 Частота опроса (Polling Rate): Отслеживание событий интерфейса Avalonia UI";
        }
    }
}
