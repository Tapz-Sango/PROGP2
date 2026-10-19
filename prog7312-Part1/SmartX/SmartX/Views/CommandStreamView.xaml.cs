using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartX.Models;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class CommandStreamView : UserControl
    {
        private readonly ApiService _api = new();
        private readonly DispatcherTimer _autoStream = new() { Interval = TimeSpan.FromMilliseconds(1800) };
        private List<Device> _allDevices = new();
        private readonly Random _rnd = new();

        // Guards against the filter firing before the view is fully initialised.
        private bool _isReady;

        public CommandStreamView()
        {
            InitializeComponent();

            _autoStream.Tick += AutoStream_Tick;

            Loaded += async (_, __) =>
            {
                await LoadDevicesAsync();
                await LoadHistoryAsync();
                await LoadInsightsAsync();
                _isReady = true;   // only NOW do we allow filtering
            };

            Unloaded += (_, __) => _autoStream.Stop();
        }

        private async System.Threading.Tasks.Task LoadDevicesAsync()
        {
            _allDevices = await _api.GetDevicesAsync() ?? new List<Device>();
            ApplyFilter();
        }

        private async System.Threading.Tasks.Task LoadHistoryAsync()
        {
            var history = await _api.GetCommandHistoryAsync();
            LstHistory.ItemsSource = history;
            TxtUndoHint.Text = $"Stack: {history.Count} command(s)";
        }

        private async System.Threading.Tasks.Task LoadInsightsAsync()
        {
            var insights = await _api.GetInsightsAsync();
            LstInsights.ItemsSource = insights;
        }

        // ---------------------------------------------------------------
        // Guarded filter. Safe to call from constructor-time events.
        // ---------------------------------------------------------------
        private void ApplyFilter()
        {
            // 1) Not initialised yet → do nothing. WPF may fire
            //    SelectionChanged / TextChanged before fields exist.
            if (!_isReady || TxtSearch == null || DgDevices == null) return;

            // 2) Null-safe device list (defensive — LoadDevicesAsync
            //    always sets this, but the guard costs nothing).
            var devices = _allDevices ?? new List<Device>();

            string term = TxtSearch.Text?.Trim() ?? "";
            string? cat = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string? state = (CmbAlertState?.SelectedItem as ComboBoxItem)?.Content?.ToString();

            var filtered = devices.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                filtered = filtered.Where(d =>
                    (d.MacAddress ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (d.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (d.Location ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(cat) && cat != "All")
                filtered = filtered.Where(d => (d.Category ?? "").Equals(cat, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(state) && state != "All")
                filtered = filtered.Where(d => (d.AlertState ?? "").Equals(state, StringComparison.OrdinalIgnoreCase));

            DgDevices.ItemsSource = filtered.ToList();
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();

            // Only hit the predictive engine once the user is really typing
            // something meaningful, and only after the view is ready.
            if (!_isReady) return;

            var term = TxtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(term) || term.Length < 3) return;

            var cat = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            var suggestions = await _api.LogSearchAsync(term, cat);
            var insights = await _api.GetInsightsAsync();

            var merged = suggestions.Concat(insights).ToList();
            LstInsights.ItemsSource = merged;
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;   // prevent firing before Loaded
            ApplyFilter();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
            await LoadInsightsAsync();
        }

        // ---- Command issuing ----
        private async void BtnPumpOn_Click(object sender, RoutedEventArgs e) => await IssueCommandAsync("PUMP_ON");
        private async void BtnPumpOff_Click(object sender, RoutedEventArgs e) => await IssueCommandAsync("PUMP_OFF");
        private async void BtnRelayToggle_Click(object sender, RoutedEventArgs e) => await IssueCommandAsync("RELAY_TOGGLE");

        private async System.Threading.Tasks.Task IssueCommandAsync(string command)
        {
            if (DgDevices.SelectedItem is not Device dev)
            {
                MessageBox.Show("Select a device first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool ok = await _api.IssueCommandAsync(dev.MacAddress, command, dev.AlertState);
            if (ok)
            {
                await LoadHistoryAsync();
                await LoadInsightsAsync();
            }
            else
            {
                MessageBox.Show("Command failed. Check API connectivity.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var result = await _api.UndoLastCommandAsync();
            MessageBox.Show(result?.Message ?? "Nothing to undo.", "Undo", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadHistoryAsync();
        }

        // ---- Simulator ----
        private async void BtnSimulateNormal_Click(object sender, RoutedEventArgs e)
            => await SendRandomTelemetryAsync(forceCritical: false);

        private async void BtnSimulateCritical_Click(object sender, RoutedEventArgs e)
            => await SendRandomTelemetryAsync(forceCritical: true);

        private async void BtnStartAuto_Click(object sender, RoutedEventArgs e)
        {
            _autoStream.Start();
            MessageBox.Show("Auto-stream started. Normal telemetry every ~2s, random critical spike every ~6s.",
                "Simulator", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnStopAuto_Click(object sender, RoutedEventArgs e) => _autoStream.Stop();

        private async void AutoStream_Tick(object? sender, EventArgs e)
        {
            bool critical = _rnd.NextDouble() < 0.30;
            await SendRandomTelemetryAsync(critical);
        }

        private async System.Threading.Tasks.Task SendRandomTelemetryAsync(bool forceCritical)
        {
            if (_allDevices == null || _allDevices.Count == 0) return;
            var dev = _allDevices[_rnd.Next(_allDevices.Count)];

            double value;
            string unit;
            if (dev.Category != null && dev.Category.Contains("Moisture", StringComparison.OrdinalIgnoreCase))
            {
                value = forceCritical ? _rnd.Next(5, 19) : _rnd.Next(30, 80);
                unit = "%";
            }
            else if (dev.Category != null && dev.Category.Contains("Power", StringComparison.OrdinalIgnoreCase))
            {
                value = forceCritical ? _rnd.Next(2100, 3500) : _rnd.Next(300, 1800);
                unit = "W";
            }
            else if (dev.Category != null && dev.Category.Contains("Temperature", StringComparison.OrdinalIgnoreCase))
            {
                value = forceCritical ? _rnd.Next(36, 48) : _rnd.Next(18, 32);
                unit = "°C";
            }
            else
            {
                value = _rnd.Next(0, 100);
                unit = "";
            }

            await _api.IngestTelemetryAsync(dev.MacAddress, value, unit, forceCritical);

            await LoadDevicesAsync();
            await UpdateQueuePillsAsync();
            await LoadInsightsAsync();
        }

        private async System.Threading.Tasks.Task UpdateQueuePillsAsync()
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:5245") };
                if (!string.IsNullOrEmpty(AuthSession.Token))
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthSession.Token);

                var res = await http.GetFromJsonAsync<QueueSnapshot>("/api/telemetry/queue");
                if (res != null)
                {
                    TxtCriticalCount.Text = $"CRITICAL: {res.Critical?.Count ?? 0}";
                    TxtFifoCount.Text = $"FIFO: {res.Standard?.Count ?? 0}";
                }
            }
            catch { /* silent — pills are cosmetic */ }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _autoStream.Stop();
            MainWindow.Instance!.NavigateTo(new GatewayMenuView());
        }

        private class QueueSnapshot
        {
            public List<object>? Standard { get; set; }
            public List<object>? Critical { get; set; }
        }
    }
}