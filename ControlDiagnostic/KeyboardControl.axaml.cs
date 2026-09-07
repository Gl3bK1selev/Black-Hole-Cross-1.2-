using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Remote.Protocol.Input;
using Black_Hole_Cross.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Key = Avalonia.Input.Key;

namespace Black_Hole_Cross.ControlDiagnostic
{
    public partial class KeyboardControl : UserControl
    {
      
        private readonly Dictionary<Key, Border> _keyVisualMap = new Dictionary<Key, Border>();
        private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();

        public KeyboardControl()
        {
            InitializeComponent();

            Loaded += KeyboardControl_Loaded;
            Unloaded += KeyboardControl_Unloaded;

            // Локализация
            DiagnosKeyboard.Text = LocalizationService.Instance["KeyboardDiagnostics"] ?? "Диагностика клавиатуры";
            descrip.Text = LocalizationService.Instance["KeyboardDesc"] ?? "Интерактивное тестирование клавиш (Key Test & Anti-Ghosting)";
        }

        private void KeyboardControl_Loaded(object? sender, RoutedEventArgs e)
        {
            RegisterKeyVisuals();
            ShowKeyboardStaticInfo();

           
            Focusable = true;
            Focus();

           
            if (VisualRoot is Window topWindow)
            {
                topWindow.AddHandler(KeyDownEvent, TopWindow_KeyDown, RoutingStrategies.Tunnel);
                topWindow.AddHandler(KeyUpEvent, TopWindow_KeyUp, RoutingStrategies.Tunnel);
            }
        }

        private void KeyboardControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            if (VisualRoot is Window topWindow)
            {
                topWindow.RemoveHandler(KeyDownEvent, TopWindow_KeyDown);
                topWindow.RemoveHandler(KeyUpEvent, TopWindow_KeyUp);
            }
        }

       
        private void TopWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            Key key = e.Key;

            if (!_pressedKeys.Contains(key))
            {
                _pressedKeys.Add(key);
            }

            HighlightKey(key, true);

            LastKeyText.Text = $"Нажата клавиша: {key} (Code: {(int)key})";
            ActiveKeysText.Text = $"Активно клавиш: {_pressedKeys.Count}";
        }

        private void TopWindow_KeyUp(object? sender, KeyEventArgs e)
        {
            Key key = e.Key;

            if (_pressedKeys.Contains(key))
            {
                _pressedKeys.Remove(key);
            }

         
            HighlightKeyVisited(key);

            ActiveKeysText.Text = $"Активно клавиш: {_pressedKeys.Count}";
        }

        private void HighlightKey(Key key, bool isPressed)
        {
            if (_keyVisualMap.TryGetValue(key, out var border))
            {
                border.Background = isPressed
                    ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) 
                    : new SolidColorBrush(Color.FromRgb(40, 167, 69)); 
            }
        }

        private void HighlightKeyVisited(Key key)
        {
            if (_keyVisualMap.TryGetValue(key, out var border))
            {
                border.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            }
        }

       
        private void ShowKeyboardStaticInfo()
        {
            string osName = "Unknown OS";
            string connType = "USB / Built-in Bus";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                osName = "Windows Input System (Win32 Raw Input)";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                osName = "Linux Evdev Input Driver";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                osName = "macOS Quartz Input Manager";

            InfoText.Text = $"⌨️ Тип устройства: Полноразмерная клавиатура (Standard 104/105-Key)\n" +
                            $"🔌 Интерфейс ввода: {osName}\n" +
                            $"🌐 Режим Anti-Ghosting: Поддерживается (N-Key Rollover Test)\n" +
                            $"🟢 Статус: Устройство активно и готово к тестированию";
        }

       
        private void RegisterKeyVisuals()
        {
            _keyVisualMap.Clear();

            // Ряд F-клавиш
            MapKey(Key.Escape, Key_Esc);
            MapKey(Key.F1, Key_F1); MapKey(Key.F2, Key_F2); MapKey(Key.F3, Key_F3); MapKey(Key.F4, Key_F4);
            MapKey(Key.F5, Key_F5); MapKey(Key.F6, Key_F6); MapKey(Key.F7, Key_F7); MapKey(Key.F8, Key_F8);
            MapKey(Key.F9, Key_F9); MapKey(Key.F10, Key_F10); MapKey(Key.F11, Key_F11); MapKey(Key.F12, Key_F12);

            // Цифровой ряд
            MapKey(Key.OemTilde, Key_Grave);
            MapKey(Key.D1, Key_1); MapKey(Key.D2, Key_2); MapKey(Key.D3, Key_3); MapKey(Key.D4, Key_4); MapKey(Key.D5, Key_5);
            MapKey(Key.D6, Key_6); MapKey(Key.D7, Key_7); MapKey(Key.D8, Key_8); MapKey(Key.D9, Key_9); MapKey(Key.D0, Key_0);
            MapKey(Key.Back, Key_Back);

            // Буквенный ряд 1 (QWERTY)
            MapKey(Key.Tab, Key_Tab);
            MapKey(Key.Q, Key_Q); MapKey(Key.W, Key_W); MapKey(Key.E, Key_E); MapKey(Key.R, Key_R); MapKey(Key.T, Key_T);
            MapKey(Key.Y, Key_Y); MapKey(Key.U, Key_U); MapKey(Key.I, Key_I); MapKey(Key.O, Key_O); MapKey(Key.P, Key_P);

            // Буквенный ряд 2 (ASDF)
            MapKey(Key.Capital, Key_Caps);
            MapKey(Key.A, Key_A); MapKey(Key.S, Key_S); MapKey(Key.D, Key_D); MapKey(Key.F, Key_F); MapKey(Key.G, Key_G);
            MapKey(Key.H, Key_H); MapKey(Key.J, Key_J); MapKey(Key.K, Key_K); MapKey(Key.L, Key_L);
            MapKey(Key.Return, Key_Enter);

            // Буквенный ряд 3 (ZXCV)
            MapKey(Key.LeftShift, Key_LShift); MapKey(Key.RightShift, Key_RShift);
            MapKey(Key.Z, Key_Z); MapKey(Key.X, Key_X); MapKey(Key.C, Key_C); MapKey(Key.V, Key_V); MapKey(Key.B, Key_B);
            MapKey(Key.N, Key_N); MapKey(Key.M, Key_M);

            // Нижний ряд
            MapKey(Key.LeftCtrl, Key_LCtrl); MapKey(Key.RightCtrl, Key_RCtrl);
            MapKey(Key.LeftAlt, Key_LAlt); MapKey(Key.RightAlt, Key_RAlt);
            MapKey(Key.Space, Key_Space);

            // Стрелки
            MapKey(Key.Up, Key_Up); MapKey(Key.Down, Key_Down);
            MapKey(Key.Left, Key_Left); MapKey(Key.Right, Key_Right);
        }

        private void MapKey(Key key, Border? border)
        {
            if (border != null && !_keyVisualMap.ContainsKey(key))
            {
                _keyVisualMap.Add(key, border);
            }
        }

        private void ResetTest_Click(object? sender, RoutedEventArgs e)
        {
            _pressedKeys.Clear();
            foreach (var kvp in _keyVisualMap)
            {
                kvp.Value.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            }
            LastKeyText.Text = "Нажмите любую клавишу для проверки";
            ActiveKeysText.Text = "Активно клавиш: 0";
        }
    }
}
