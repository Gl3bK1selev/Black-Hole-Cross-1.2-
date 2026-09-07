using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Black_Hole_Cross
{
    public partial class UpdateCheckWindow : Window
    {
        public UpdateCheckWindow()
        {
            InitializeComponent();
        }

        private async void CheckUpdateBtn_Click(object? sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;

        
            if (StatusTextBlock != null)
                StatusTextBlock.Text = "Проверка обновлений...";

            await Task.Delay(2000);

            CheckUpdateBtn.IsEnabled = true;

            // Показываем текст прямо на форме
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = "Black Hole v1.0 — это первая версия приложения.\nВсе обновления будут анонсированы здесь.";
            }
        }

        private void CloseBtn_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}