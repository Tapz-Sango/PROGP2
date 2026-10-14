using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SmartX.Models;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class SensorIngestionView : UserControl
    {
        private readonly ApiService _apiService = new();
        private readonly List<SensorRegistration> _localSensors = new();
        private string _selectedFilePath = string.Empty;

        public SensorIngestionView()
        {
            InitializeComponent();
            LoadSensors();
        }

        private async void LoadSensors()
        {
            try
            {
                var sensors = await _apiService.GetSensorsAsync();
                if (sensors != null && sensors.Count > 0)
                {
                    _localSensors.Clear();
                    _localSensors.AddRange(sensors);
                }
                RefreshGrid();
            }
            catch
            {
                // Local fallback initialization if API service is offline
                if (_localSensors.Count == 0)
                {
                    _localSensors.Add(new SensorRegistration
                    {
                        MacAddress = "A4:C1:38:9B:4F:10",
                        Location = "Hydroponic Zone B",
                        Category = "Environmental"
                    });
                    _localSensors.Add(new SensorRegistration
                    {
                        MacAddress = "C8:2B:96:11:02:AA",
                        Location = "Utility Grid Meter 1",
                        Category = "Power Consumption"
                    });
                }
                RefreshGrid();
            }
        }

        private void RefreshGrid()
        {
            DgSensors.ItemsSource = null;
            DgSensors.ItemsSource = _localSensors;
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string mac = TxtMac.Text.Trim();
            string location = TxtLocation.Text.Trim();
            string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Environmental";

            if (!Regex.IsMatch(mac, "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
            {
                MessageBox.Show("Client Validation Error: MAC address must follow format XX:XX:XX:XX:XX:XX", "Validation Failure", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(location))
            {
                MessageBox.Show("Please enter a deployment location.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sensor = new SensorRegistration
            {
                MacAddress = mac,
                Location = location,
                Category = category
            };

            _localSensors.Add(sensor);
            RefreshGrid();

            TxtMac.Clear();
            TxtLocation.Clear();

            bool success = await _apiService.RegisterSensorAsync(sensor);
            if (success)
            {
                MessageBox.Show("Sensor Payload Ingested via API Successfully!", "API Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRandomSeed_Click(object sender, RoutedEventArgs e)
        {
            Random rnd = new();
            byte[] bytes = new byte[6];
            rnd.NextBytes(bytes);

            string mac = string.Join(":", bytes.Select(b => b.ToString("X2")));
            string location = $"Zone {rnd.Next(1, 10)} - SubFacility {rnd.Next(100, 999)}";
            string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Environmental";

            var sensor = new SensorRegistration
            {
                MacAddress = mac,
                Location = location,
                Category = category
            };

            _localSensors.Add(sensor);
            RefreshGrid();
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Config & Log Files (*.txt;*.log;*.json;*.png)|*.txt;*.log;*.json;*.png|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                TxtFilePath.Text = Path.GetFileName(_selectedFilePath);
            }
        }

        private async void BtnUploadFile_Click(object sender, RoutedEventArgs e)
        {
            if (DgSensors.SelectedItem is not SensorRegistration selectedSensor)
            {
                MessageBox.Show("Please select a target sensor from the grid to attach the config/log file.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Please select a configuration file first.", "File Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool uploaded = await _apiService.UploadDeviceFileAsync(selectedSensor.MacAddress, _selectedFilePath);
            if (uploaded)
            {
                MessageBox.Show("File uploaded and AES encrypted stream saved on API backend!", "Upload Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _selectedFilePath = string.Empty;
                TxtFilePath.Text = "No file attached";
            }
            else
            {
                MessageBox.Show("Failed to stream encrypted file to API.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnValidateTree_Click(object sender, RoutedEventArgs e)
        {
            var rootFacility = new DeploymentNode("Main Hydroponics Hub", "Facility A", true);
            var zone1 = new DeploymentNode("Zone 1 Automated Greenhouse", "Zone 1", true, rootFacility);
            var subZoneB = new DeploymentNode("Sub-Zone B Micro-controllers", "Sub-Zone B", true, zone1);
            var nodeESP = new DeploymentNode("ESP32-Soil-Node-09", "Node ID", true, subZoneB);

            bool isValid = DeploymentNode.RecursiveValidateHierarchy(nodeESP, out string reason);

            if (isValid)
            {
                MessageBox.Show($"Recursive Validation Success!\nNode hierarchy [{nodeESP.Name}] is safely nested in [Facility A]. Base Case reached.", "Hierarchy Audit Passed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Validation Failure: {reason}", "Audit Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => MainWindow.Instance!.NavigateTo(new GatewayMenuView());
        private void BtnOpenTelemetry_Click(object sender, RoutedEventArgs e) => MainWindow.Instance!.NavigateTo(new TelemetryStreamView());
    }
}