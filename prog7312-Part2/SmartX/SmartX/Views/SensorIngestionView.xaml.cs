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

        // -----------------------------------------------------------------
        // Pulls the device registry from the API (backed by a Dictionary on
        // the server) and projects it into SensorRegistration rows for the
        // grid. Falls back to two seed rows if the API is unreachable.
        // -----------------------------------------------------------------
        private async void LoadSensors()
        {
            try
            {
                var devices = await _apiService.GetDevicesAsync();

                _localSensors.Clear();

                if (devices != null && devices.Count > 0)
                {
                    foreach (var d in devices)
                    {
                        _localSensors.Add(new SensorRegistration
                        {
                            MacAddress = d.MacAddress,
                            // Show "Name @ Location" so the grid stays informative
                            Location = string.IsNullOrWhiteSpace(d.Name)
                                ? d.Location
                                : $"{d.Name} @ {d.Location}",
                            Category = d.Category,
                            RegisteredAt = d.RegisteredAt == default
                                ? DateTime.UtcNow
                                : d.RegisteredAt
                        });
                    }
                }
                else
                {
                    // API reachable but empty — seed the local grid for demo
                    SeedLocalSensors();
                }
            }
            catch
            {
                // API offline — keep whatever local rows we already have,
                // otherwise seed the demo set so the grid isn't blank.
                if (_localSensors.Count == 0)
                {
                    SeedLocalSensors();
                }
            }
            finally
            {
                RefreshGrid();
            }
        }

        // -----------------------------------------------------------------
        // Two demo rows used when the API is offline / empty.
        // -----------------------------------------------------------------
        private void SeedLocalSensors()
        {
            _localSensors.Add(new SensorRegistration
            {
                MacAddress = "A4:C1:38:9B:4F:10",
                Location = "Hydroponic Zone B",
                Category = "Moisture",
                RegisteredAt = DateTime.UtcNow
            });
            _localSensors.Add(new SensorRegistration
            {
                MacAddress = "C8:2B:96:11:02:AA",
                Location = "Utility Grid Meter 1",
                Category = "Power",
                RegisteredAt = DateTime.UtcNow
            });
        }

        private void RefreshGrid()
        {
            DgSensors.ItemsSource = null;
            DgSensors.ItemsSource = _localSensors;
        }

        // -----------------------------------------------------------------
        // Register a new device against the API. Client-side validation
        // runs first (full MAC regex), then the API re-validates.
        // -----------------------------------------------------------------
        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string mac = TxtMac.Text.Trim();
            string location = TxtLocation.Text.Trim();
            string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Environmental";

            // Full-format MAC check (Part 1 feedback fix)
            if (!Regex.IsMatch(mac, "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
            {
                MessageBox.Show(
                    "Client Validation Error: MAC address must follow format XX:XX:XX:XX:XX:XX",
                    "Validation Failure",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(location))
            {
                MessageBox.Show(
                    "Please enter a deployment location.",
                    "Validation Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Optimistic UI: show the row straight away
            var localRow = new SensorRegistration
            {
                MacAddress = mac,
                Location = location,
                Category = category,
                RegisteredAt = DateTime.UtcNow
            };
            _localSensors.Add(localRow);
            RefreshGrid();

            TxtMac.Clear();
            TxtLocation.Clear();

            // Shape must match the API's DeviceRecord. Name is auto-filled
            // from the MAC so the Command Stream view has something to show.
            var payload = new
            {
                MacAddress = mac,
                Name = mac,
                Location = location,
                Category = category,
                AlertState = "Normal"
            };

            bool success = await _apiService.RegisterSensorAsync(payload);

            if (success)
            {
                MessageBox.Show(
                    "Sensor registered via API successfully!",
                    "API Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Confirm with a fresh pull so the grid reflects server truth
                await System.Threading.Tasks.Task.Delay(300);
                LoadSensors();
            }
            else
            {
                MessageBox.Show(
                    "Local row added, but the API rejected or was unreachable.",
                    "API Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // -----------------------------------------------------------------
        // Random MAC generator — unchanged from Part 1, kept for demos.
        // -----------------------------------------------------------------
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
                Category = category,
                RegisteredAt = DateTime.UtcNow
            };

            _localSensors.Add(sensor);
            RefreshGrid();
        }

        // -----------------------------------------------------------------
        // File picker — accepts .txt (device list), .csv, .log, .json, images
        // -----------------------------------------------------------------
        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Device Lists (*.txt;*.csv)|*.txt;*.csv|" +
                         "Config & Log Files (*.log;*.json)|*.log;*.json|" +
                         "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|" +
                         "All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                TxtFilePath.Text = Path.GetFileName(_selectedFilePath);
            }
        }

        // -----------------------------------------------------------------
        // Upload + encrypt. If the file is a .txt/.csv device list, the API
        // parses it, inserts each valid row into its Dictionary registry,
        // and we then re-pull so the grid updates automatically.
        // -----------------------------------------------------------------
        private async void BtnUploadFile_Click(object sender, RoutedEventArgs e)
        {
            if (DgSensors.SelectedItem is not SensorRegistration selectedSensor)
            {
                MessageBox.Show(
                    "Please select a target sensor from the grid to attach the config/log file.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show(
                    "Please select a configuration file first.",
                    "File Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var (uploaded, message) = await _apiService.UploadDeviceFileAsync(
                selectedSensor.MacAddress,
                _selectedFilePath);

            if (uploaded)
            {
                MessageBox.Show(
                    $"File uploaded and AES-256 encrypted on the API backend.\n\n{message}",
                    "Upload Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _selectedFilePath = string.Empty;
                TxtFilePath.Text = "No file attached";

                // If the uploaded file was a device list, the API has now
                // inserted the rows into its Dictionary — re-pull to show them.
                await System.Threading.Tasks.Task.Delay(400);
                LoadSensors();
            }
            else
            {
                MessageBox.Show(
                    $"Failed to stream encrypted file to API.\n\n{message}",
                    "Upload Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // -----------------------------------------------------------------
        // Recursive hierarchy audit — unchanged from Part 1.
        // -----------------------------------------------------------------
        private void BtnValidateTree_Click(object sender, RoutedEventArgs e)
        {
            var rootFacility = new DeploymentNode("Main Hydroponics Hub", "Facility A", true);
            var zone1 = new DeploymentNode("Zone 1 Automated Greenhouse", "Zone 1", true, rootFacility);
            var subZoneB = new DeploymentNode("Sub-Zone B Micro-controllers", "Sub-Zone B", true, zone1);
            var nodeESP = new DeploymentNode("ESP32-Soil-Node-09", "Node ID", true, subZoneB);

            bool isValid = DeploymentNode.RecursiveValidateHierarchy(nodeESP, out string reason);

            if (isValid)
            {
                MessageBox.Show(
                    $"Recursive Validation Success!\nNode hierarchy [{nodeESP.Name}] is safely nested in [Facility A]. Base Case reached.",
                    "Hierarchy Audit Passed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Validation Failure: {reason}",
                    "Audit Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // -----------------------------------------------------------------
        // Navigation
        // -----------------------------------------------------------------
        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance!.NavigateTo(new GatewayMenuView());

        private void BtnOpenTelemetry_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance!.NavigateTo(new TelemetryStreamView());
    }
}