using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class MeshMatrixView : UserControl
    {
        private readonly DispatcherTimer _timer = new();
        private readonly ApiService _apiService = new();
        private readonly List<string> _userReportedIssuesList = new();

        public MeshMatrixView()
        {
            InitializeComponent();

            _timer.Interval = TimeSpan.FromMilliseconds(1500);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Random rnd = new();

            double temp = Math.Round(20.0 + rnd.NextDouble() * 20.0, 1);
            Val1.Text = $"Telemetry: {temp} °C";
            if (temp > 35.0)
            {
                Node1.BorderBrush = Brushes.Red;
                Dot1.Fill = Brushes.Red;
            }
            else
            {
                Node1.BorderBrush = (Brush)Application.Current.Resources["AccentCyan"];
                Dot1.Fill = (Brush)Application.Current.Resources["AccentCyan"];
            }

            int watts = rnd.Next(400, 2500);
            Val2.Text = $"Telemetry: {watts} W";
            if (watts > 2000)
            {
                Node2.BorderBrush = Brushes.Red;
                Dot2.Fill = Brushes.Red;
            }
            else
            {
                Node2.BorderBrush = (Brush)Application.Current.Resources["AccentCyan"];
                Dot2.Fill = (Brush)Application.Current.Resources["AccentCyan"];
            }
        }

        private async void BtnLogIssue_Click(object sender, RoutedEventArgs e)
        {
            string issue = TxtIssue.Text.Trim();
            if (string.IsNullOrEmpty(issue)) return;

            _userReportedIssuesList.Add($"[{DateTime.Now:HH:mm:ss}] {issue}");
            await _apiService.PostIssueAsync(issue);

            LstIssues.ItemsSource = null;
            LstIssues.ItemsSource = _userReportedIssuesList;
            TxtIssue.Clear();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            MainWindow.Instance!.NavigateTo(new GatewayMenuView());
        }
    }
}