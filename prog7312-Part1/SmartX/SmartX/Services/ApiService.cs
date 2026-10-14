using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SmartX.Models;

namespace SmartX.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;

        // Base address of the API; port must match the backend
        private const string BaseUrl = "http://localhost:5245";

        public ApiService()
        {
            _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            // Give up on a request if it takes longer than 10 seconds
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        // Sends login credentials and returns true if the API accepts them
        public async Task<bool> AuthenticateAsync(string email, string password)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Fetches all registered sensors; returns an empty list on failure
        public async Task<List<SensorRegistration>> GetSensorsAsync()
        {
            try
            {
                // This will fail if the SensorRecord structure in API doesn't match SensorRegistration in WPF exactly
                return await _http.GetFromJsonAsync<List<SensorRegistration>>("/api/sensors") ?? new List<SensorRegistration>();
            }
            catch
            {
                return new List<SensorRegistration>();
            }
        }

        // Sends a new sensor registration to the API
        public async Task<bool> RegisterSensorAsync(SensorRegistration sensor)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("/api/sensors", sensor);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Uploads a device file to the API for a given sensor, using a multipart form
        public async Task<bool> UploadDeviceFileAsync(string macAddress, string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                // Multipart request structure for the API endpoint
                using var content = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(filePath);
                using var streamContent = new StreamContent(fileStream);

                content.Add(streamContent, "file", Path.GetFileName(filePath));
                var response = await _http.PostAsync($"/api/sensors/{macAddress}/upload", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Reports a new issue to the API
        public async Task<bool> PostIssueAsync(string description)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/api/issues", new { Description = description });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}