using System.Windows;
using System.Windows.Controls;

namespace SmartX.Views
{
    public partial class GatewayMenuView : UserControl
    {
        public GatewayMenuView()
        {
            InitializeComponent();
        }

        private void BtnSensorIngestion_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance!.NavigateTo(new SensorIngestionView());
        }

        private void BtnTelemetryStream_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance!.NavigateTo(new TelemetryStreamView());
        }

        private void BtnMeshMatrix_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance!.NavigateTo(new MeshMatrixView());
        }
    }
}