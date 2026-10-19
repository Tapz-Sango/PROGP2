using System.Collections.Concurrent;

namespace SmartX.Api.DataStructures;

// Dictionary<TKey,TValue> — O(1) device lookup by MAC (Part 2 requirement)
public class DeviceRegistry
{
    private readonly ConcurrentDictionary<string, DeviceRecord> _devices = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _devices.Count;
    public IEnumerable<DeviceRecord> All => _devices.Values;

    public bool TryGet(string mac, out DeviceRecord? device) => _devices.TryGetValue(Normalise(mac), out device);

    public DeviceRecord Upsert(DeviceRecord device)
    {
        device.MacAddress = Normalise(device.MacAddress);
        _devices.AddOrUpdate(device.MacAddress, device, (_, existing) =>
        {
            existing.Location = device.Location;
            existing.Category = device.Category;
            existing.LastSeen = DateTime.UtcNow;
            return existing;
        });
        return _devices[device.MacAddress];
    }

    public bool Remove(string mac) => _devices.TryRemove(Normalise(mac), out _);

    public IEnumerable<DeviceRecord> FilterByCategory(string? category) =>
        string.IsNullOrWhiteSpace(category)
            ? _devices.Values
            : _devices.Values.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<DeviceRecord> FilterByAlertState(string? state) =>
        string.IsNullOrWhiteSpace(state) || state == "All"
            ? _devices.Values
            : _devices.Values.Where(d => d.AlertState.Equals(state, StringComparison.OrdinalIgnoreCase));

    private static string Normalise(string mac) => (mac ?? "").Replace('-', ':').Trim().ToUpperInvariant();
}

public class DeviceRecord
{
    public string MacAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AlertState { get; set; } = "Normal"; // Normal | Warning | Critical
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public double LastValue { get; set; }
    public string LastUnit { get; set; } = string.Empty;
}