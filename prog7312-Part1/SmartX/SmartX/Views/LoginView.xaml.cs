using System.Windows;
using System.Windows.Controls;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class LoginView : UserControl
    {
        private readonly ApiService _apiService = new();

        public LoginView()
        {
            InitializeComponent();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            bool success = await _apiService.AuthenticateAsync(TxtEmail.Text, TxtPassword.Password);

            if (success)
            {
                MainWindow.Instance!.NavigateTo(new GatewayMenuView());
            }
            else
            {
                TxtError.Text = "Invalid API Authentication Credentials";
                TxtError.Visibility = Visibility.Visible;
            }
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance!.NavigateTo(new GatewayMenuView());
        }
    }
}