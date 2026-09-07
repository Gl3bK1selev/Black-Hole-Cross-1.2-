using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Black_Hole_Cross.Panels
{
  
    public partial class Help_Panel : UserControl
    {
        public Help_Panel()
        {
            InitializeComponent();
        }

        private async void SendFeedbackButton_Click(object? sender, RoutedEventArgs e)
        {
          
            if (string.IsNullOrWhiteSpace(NameTextBox?.Text) ||
                string.IsNullOrWhiteSpace(EmailTextBox?.Text) ||
                string.IsNullOrWhiteSpace(MessageTextBox?.Text))
            {
                await ShowMessageBoxAsync("Внимание", "Пожалуйста, заполните все поля!");
                return;
            }

            SendFeedbackButton.IsEnabled = false;
            SendFeedbackButton.Content = "Отправка...";

            try
            {
               
                await Task.Delay(1000); 
                await ShowMessageBoxAsync("Успешно", "Спасибо за ваш отзыв! Сообщение отправлено.");

              
                NameTextBox.Text = string.Empty;
                EmailTextBox.Text = string.Empty;
                MessageTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync("Ошибка", $"Ошибка отправки: {ex.Message}");
            }
            finally
            {
                SendFeedbackButton.IsEnabled = true;
                SendFeedbackButton.Content = "Отправить сообщение";
            }
        }

      
        private async Task ShowMessageBoxAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 380,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = (IBrush)this.FindResource("CardBg")! ?? Brushes.DarkGray,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = (IBrush)this.FindResource("TextPrimary")! ?? Brushes.White,
                            FontSize = 14
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Padding = new Avalonia.Thickness(15, 5),
                            Background = (IBrush)this.FindResource("HoverBg")! ?? Brushes.Gray,
                            Foreground = Brushes.White
                        }
                    }
                }
            };

          
            if (dialog.Content is StackPanel panel && panel.Children[1] is Button okButton)
            {
                okButton.Click += (_, _) => dialog.Close();
            }

           
            if (TopLevel.GetTopLevel(this) is Window parentWindow)
            {
                await dialog.ShowDialog(parentWindow);
            }
            else
            {
                dialog.Show();
            }
        }
    }
}



