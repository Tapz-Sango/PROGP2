using System.Windows;
using System.Windows.Controls;

namespace SmartX.Views
{
    public partial class LandingView : UserControl
    {
        public LandingView() => InitializeComponent();

        private void BtnEnter_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance!.NavigateTo(new LoginView());
        }
    }
}