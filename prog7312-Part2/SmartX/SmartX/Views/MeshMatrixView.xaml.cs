using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class MeshMatrixView : UserControl
    {
        // ---------------------------------------------------------------
        // DispatcherTimer drives the simulated mesh packet loop.
        // Every tick we generate a fresh reading for each online node,
        // push it to the API (as a real device would), then evaluate
        // local thresholds to decide whether to flash the node red.
        // ---------------------------------------------------------------
        private readonly DispatcherTimer _timer = new();
        private readonly ApiService _apiService = new();
        private readonly Random _rnd = new();

        // ObservableCollection so the ListBox updates as alerts arrive.
        private readonly ObservableCollection<AlertRow> _alerts = new();

        // HashSet semantics on the client: we suppress the same alert
        // from appearing twice within a short window. The API already
        // deduplicates on its side using its own HashSet, but keeping a
        // client-side guard keeps the UI tidy when the timer fires fast.
        private readonly System.Collections.Generic.HashSet<string> _recentKeys = new();

        // MACs of the three online nodes in the radar view.
        private const string Node1Mac = "AD:76:E3:5F:59:93"; // Temperature
        private const string Node2Mac = "C8:2B:96:11:02:AA"; // Power
        private const string Node3Mac = "1C:46:79:A5:5C:E5"; // Actuator (Pump)

        public MeshMatrixView()
        {
            InitializeComponent();

            LstIssues.ItemsSource = _alerts;

            _timer.Interval = TimeSpan.FromMilliseconds(1500);
            _timer.Tick += Timer_Tick;

            Loaded += (_, __) => _timer.Start();
            Unloaded += (_, __) => _timer.Stop();
        }

        // ---------------------------------------------------------------
        // Every 1.5s: generate readings, push them to the API, update UI,
        // and raise automatic alerts when thresholds are breached.
        // ---------------------------------------------------------------
        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // ---------------- Node 1: Temperature ----------------
            double temp = Math.Round(18.0 + _rnd.NextDouble() * 22.0, 1); // 18 - 40 °C
            Val1.Text = $"Telemetry: {temp} °C";

            bool tempAlert = temp > 35.0;
            ApplyNodeState(Node1, Dot1, tempAlert);

            // Push to API regardless — this is what a real device would do.
            await _apiService.IngestTelemetryAsync(Node1Mac, temp, "°C", tempAlert);

            if (tempAlert)
            {
                RaiseAlert(
                    nodeName: "NODE-ESP-01",
                    mac: Node1Mac,
                    message: $"Temperature {temp} °C exceeds safe threshold of 35 °C",
                    severity: "Critical");
            }

            // ---------------- Node 2: Power ----------------
            int watts = _rnd.Next(400, 2500); // 400 - 2500 W
            Val2.Text = $"Telemetry: {watts} W";

            bool powerAlert = watts > 2000;
            ApplyNodeState(Node2, Dot2, powerAlert);

            await _apiService.IngestTelemetryAsync(Node2Mac, watts, "W", powerAlert);

            if (powerAlert)
            {
                RaiseAlert(
                    nodeName: "NODE-ESP-02",
                    mac: Node2Mac,
                    message: $"Power spike {watts} W exceeds safe threshold of 2000 W",
                    severity: "Critical");
            }

            // ---------------- Node 3: Actuator heartbeat ----------------
            // Valve state toggles occasionally to simulate real activity.
            bool valveActive = _rnd.NextDouble() > 0.15;
            Val3.Text = $"Valve: {(valveActive ? "ACTIVE" : "IDLE")}";
            ApplyNodeState(Node3, Dot3, alert: false); // actuator itself never alerts here

            // Actuator heartbeat value: 1 = on, 0 = off.
            await _apiService.IngestTelemetryAsync(Node3Mac, valveActive ? 1 : 0, "STATE", false);

            TxtAlertCount.Text = $"Total alerts: {_alerts.Count}";
        }

        // ---------------------------------------------------------------
        // Colour the node card and status dot based on alert state.
        // ---------------------------------------------------------------
        private void ApplyNodeState(Border node, System.Windows.Shapes.Ellipse dot, bool alert)
        {
            if (alert)
            {
                node.BorderBrush = Brushes.Red;
                dot.Fill = Brushes.Red;
            }
            else
            {
                var cyan = (Brush)Application.Current.Resources["AccentCyan"];
                node.BorderBrush = cyan;
                dot.Fill = cyan;
            }
        }

        // ---------------------------------------------------------------
        // Raise a UI alert, applying the same dedup rule the API uses:
        // identical (mac + message) pairs within the same second are
        // collapsed so the feed does not spam.
        // ---------------------------------------------------------------
        private void RaiseAlert(string nodeName, string mac, string message, string severity)
        {
            // Client-side HashSet gate
            string key = $"{mac}::{message}::{DateTime.UtcNow:HH:mm}";
            if (!_recentKeys.Add(key)) return;

            _alerts.Insert(0, new AlertRow
            {
                Timestamp = DateTime.Now,
                Node = nodeName,
                MacAddress = mac,
                Message = message,
                Severity = severity,
                SeverityBrush = severity switch
                {
                    "Critical" => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                    "Warning" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                    _ => new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7))
                }
            });

            // Keep the feed bounded.
            while (_alerts.Count > 50) _alerts.RemoveAt(_alerts.Count - 1);

            // Also persist the alert on the API so it shows up in the
            // operator's history (/api/issues) — but we do NOT await it
            // because UI responsiveness matters more than order here.
            _ = _apiService.PostIssueAsync($"[{nodeName}] {message}");
        }

        // ---------------------------------------------------------------
        // Clear the local feed (does not clear the API-side log).
        // ---------------------------------------------------------------
        private void BtnClearFeed_Click(object sender, RoutedEventArgs e)
        {
            _alerts.Clear();
            _recentKeys.Clear();
            TxtAlertCount.Text = "Total alerts: 0";
        }

        // ---------------------------------------------------------------
        // Navigation back to the gateway menu.
        // ---------------------------------------------------------------
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            MainWindow.Instance!.NavigateTo(new GatewayMenuView());
        }
    }

    // ---------------------------------------------------------------
    // Simple DTO for the ListBox binding.
    // ---------------------------------------------------------------
    public class AlertRow
    {
        public DateTime Timestamp { get; set; }
        public string Node { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = "Info";
        public Brush SeverityBrush { get; set; } = Brushes.SteelBlue;
    }
}