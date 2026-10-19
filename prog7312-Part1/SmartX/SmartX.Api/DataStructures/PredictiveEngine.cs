namespace SmartX.Api.DataStructures;

// Pattern analysis over search + command history (Part 2 requirement, 30 marks)
// Uses a Dictionary-based co-occurrence matrix:
//   key = "search:<term>" or "command:<MAC>"
//   value = Dictionary<followUpKey, weight>
// Recommends the highest-weighted follow-up action.
public class PredictiveEngine
{
    private const int MaxHistory = 500;
    private readonly LinkedList<string> _eventHistory = new();
    private readonly Dictionary<string, Dictionary<string, int>> _coOccurrence = new();
    private readonly object _lock = new();

    public void RecordEvent(string eventKey)
    {
        lock (_lock)
        {
            if (_eventHistory.Count > 0)
            {
                var prev = _eventHistory.Last!.Value;
                if (!_coOccurrence.TryGetValue(prev, out var map))
                {
                    map = new Dictionary<string, int>();
                    _coOccurrence[prev] = map;
                }
                map[eventKey] = map.GetValueOrDefault(eventKey) + 1;
            }

            _eventHistory.AddLast(eventKey);
            while (_eventHistory.Count > MaxHistory) _eventHistory.RemoveFirst();
        }
    }

    public IEnumerable<Prediction> GetSuggestions(string currentContext, int topN = 5)
    {
        lock (_lock)
        {
            if (!_coOccurrence.TryGetValue(currentContext, out var map))
                return Array.Empty<Prediction>();

            return map
                .OrderByDescending(kvp => kvp.Value)
                .Take(topN)
                .Select(kvp => new Prediction
                {
                    Context = currentContext,
                    SuggestedAction = kvp.Key,
                    Confidence = kvp.Value,
                    Reason = $"You performed '{kvp.Key}' {kvp.Value} time(s) after '{currentContext}'."
                })
                .ToArray();
        }
    }

    // Highlights devices with out-of-range readings before user searches for them.
    public IEnumerable<Prediction> HighlightProblemDevices(IEnumerable<DeviceRecord> devices)
    {
        var results = new List<Prediction>();
        foreach (var d in devices)
        {
            string? issue = null;
            if (d.Category.Contains("Moisture", StringComparison.OrdinalIgnoreCase) && d.LastValue < 20)
                issue = $"Moisture below threshold ({d.LastValue:F1}%)";
            else if (d.Category.Contains("Power", StringComparison.OrdinalIgnoreCase) && d.LastValue > 2000)
                issue = $"Power spike ({d.LastValue:F0} W)";
            else if (d.Category.Contains("Temperature", StringComparison.OrdinalIgnoreCase) && d.LastValue > 35)
                issue = $"Overheating ({d.LastValue:F1} °C)";
            else if ((DateTime.UtcNow - d.LastSeen).TotalMinutes > 2)
                issue = $"Silent for {(DateTime.UtcNow - d.LastSeen).TotalMinutes:F1} min";

            if (issue != null)
            {
                results.Add(new Prediction
                {
                    Context = "Automated Insight",
                    SuggestedAction = $"Investigate {d.Name} ({d.MacAddress})",
                    Confidence = 100,
                    Reason = issue
                });
            }
        }
        return results;
    }
}

public class Prediction
{
    public string Context { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}