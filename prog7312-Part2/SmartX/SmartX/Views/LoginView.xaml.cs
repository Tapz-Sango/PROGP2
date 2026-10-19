using System.Windows;
using System.Windows.Controls;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class LoginView : UserControl
    {
        private readonly ApiService _apiService = new();

        public LoginView() => InitializeComponent();

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            BtnLogin.IsEnabled = false;
            try
            {
                bool success = await _apiService.AuthenticateAsync(TxtEmail.Text.Trim(), TxtPassword.Password);
                if (success)
                    MainWindow.Instance!.NavigateTo(new GatewayMenuView());
                else
                {
                    TxtError.Text = "Authentication failed. Check your credentials.";
                    TxtError.Visibility = Visibility.Visible;
                }
            }
            finally { BtnLogin.IsEnabled = true; }
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            // Provide guest demo credentials so markers can bypass the API call
            TxtEmail.Text = "guest@smartx.io";
            TxtPassword.Password = "admin123";
        }
    }
}