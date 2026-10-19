using Microsoft.AspNetCore.Mvc;
using SmartX.Api.DataStructures;
using SmartX.Api.Models;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

// --- DI containers as singletons ---
var auth = new AuthService();
var encryption = new EncryptionService();
var devices = new DeviceRegistry();
var telemetryQueue = new TelemetryQueue();
var criticalQueue = new CriticalAlertQueue();
var undoStack = new CommandUndoStack();
var timeline = new SensorTimeline();
var disconnected = new DisconnectedNodes();
var predictive = new PredictiveEngine();

// Seed a few devices so the dashboard is not empty on first run
devices.Upsert(new DeviceRecord { MacAddress = "A4:C1:38:9B:4F:10", Name = "Moisture Sensor B", Location = "Hydroponic Zone B", Category = "Moisture", LastValue = 45.2, LastUnit = "%" });
devices.Upsert(new DeviceRecord { MacAddress = "C8:2B:96:11:02:AA", Name = "Utility Grid Meter 1", Location = "Utility Room", Category = "Power", LastValue = 820, LastUnit = "W" });
devices.Upsert(new DeviceRecord { MacAddress = "1C:46:79:A5:5C:E5", Name = "Pump A", Location = "Hydroponic Zone A", Category = "Actuator", LastValue = 0, LastUnit = "STATE" });
devices.Upsert(new DeviceRecord { MacAddress = "AD:76:E3:5F:59:93", Name = "Temp Sensor C", Location = "Greenhouse 3", Category = "Temperature", LastValue = 22.4, LastUnit = "°C" });

// ---------- AUTH ----------
app.MapPost("/api/auth/login", ([FromBody] LoginRequest req) =>
{
    // BOTH must match (Part 1 feedback fix)
    if (!auth.ValidateCredentials(req.Email, req.Password))
        return Results.Unauthorized();

    var token = auth.IssueToken(req.Email);
    return Results.Ok(new LoginResponse(Guid.NewGuid().ToString(), req.Email, token, DateTime.UtcNow.AddHours(4)));
});

// Token guard helper
bool IsAuthed(HttpRequest r) => auth.ValidateToken(r.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", ""));

// ---------- DEVICES (Dictionary-based lookup) ----------
app.MapGet("/api/devices", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(devices.All);
});

app.MapGet("/api/devices/filter", (HttpRequest r, string? category, string? alertState) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    var byCat = devices.FilterByCategory(category);
    var result = string.IsNullOrWhiteSpace(alertState) || alertState == "All"
        ? byCat
        : byCat.Where(d => d.AlertState.Equals(alertState, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(result);
});

app.MapGet("/api/devices/{mac}", (HttpRequest r, string mac) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    // O(1) lookup via Dictionary — no loops
    return devices.TryGet(mac, out var d) ? Results.Ok(d) : Results.NotFound();
});

// ---------- SENSOR INGESTION ----------
app.MapPost("/api/sensors", (HttpRequest r, [FromBody] DeviceRecord dto) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    if (string.IsNullOrEmpty(dto.MacAddress) || !System.Text.RegularExpressions.Regex.IsMatch(
        dto.MacAddress, "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
        return Results.BadRequest(new { message = "Invalid MAC format." });

    var saved = devices.Upsert(dto);
    return Results.Created($"/api/devices/{saved.MacAddress}", saved);
});

// File upload + AES-256 with random IV + validation (Part 1 feedback fix)
app.MapPost("/api/sensors/{mac}/upload", async (HttpRequest r, string mac, IFormFile file) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    var (ok, err) = encryption.ValidateFile(file);
    if (!ok) return Results.BadRequest(new { message = err });

    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
    var encPath = await encryption.EncryptToDiskAsync(file, uploadsFolder);

    // ---- Parse .txt uploads into devices so the grid updates (Part 1 feedback) ----
    int importedCount = 0;
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext == ".txt" || ext == ".csv")
    {
        using var reader = new StreamReader(file.OpenReadStream());
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            // Expected: MAC,Name,Location,Category
            if (parts.Length >= 4 && System.Text.RegularExpressions.Regex.IsMatch(
                parts[0], "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
            {
                devices.Upsert(new DeviceRecord
                {
                    MacAddress = parts[0],
                    Name = parts[1],
                    Location = parts[2],
                    Category = parts[3]
                });
                importedCount++;
            }
        }
    }

    return Results.Ok(new
    {
        status = "Encrypted & Stored",
        fileName = file.FileName,
        path = encPath,
        devicesImported = importedCount
    });
}).DisableAntiforgery();

// ---------- REAL-TIME TELEMETRY ----------
// Simulated device pushes a reading here
app.MapPost("/api/telemetry/ingest", (HttpRequest r, [FromBody] TelemetryIngestRequest t) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();

    var packet = new TelemetryPacketRecord
    {
        MacAddress = t.MacAddress,
        Value = t.Value,
        Unit = t.Unit,
        IsCritical = t.IsCritical
    };

    devices.TryGet(t.MacAddress, out var dev);

    // Range checks -> critical bypasses FIFO
    bool critical = t.IsCritical;
    if (dev != null)
    {
        if (dev.Category.Contains("Moisture", StringComparison.OrdinalIgnoreCase) && t.Value < 20) critical = true;
        if (dev.Category.Contains("Power", StringComparison.OrdinalIgnoreCase) && t.Value > 2000) critical = true;
        if (dev.Category.Contains("Temperature", StringComparison.OrdinalIgnoreCase) && t.Value > 35) critical = true;
    }

    // Update device cache (O(1))
    if (dev != null)
    {
        dev.LastValue = t.Value;
        dev.LastUnit = t.Unit;
        dev.LastSeen = DateTime.UtcNow;
        dev.AlertState = critical ? "Critical" : "Normal";
    }

    // SortedDictionary timeline
    timeline.Add(packet);

    if (critical)
    {
        // HashSet gate — only fire once per (mac, reason)
        var reason = $"Value {t.Value}{t.Unit} out of range";
        if (disconnected.MarkActive(t.MacAddress, reason))
        {
            criticalQueue.Enqueue(new CriticalAlert
            {
                MacAddress = t.MacAddress,
                Message = $"{dev?.Name ?? t.MacAddress}: {reason}",
                Severity = "Critical"
            }, AlertUrgency.Critical);
        }
    }
    else
    {
        telemetryQueue.Enqueue(packet);
    }

    return Results.Ok(new { queued = critical ? "CRITICAL" : "STANDARD", queueDepth = telemetryQueue.Count, alertDepth = criticalQueue.Count });
});

app.MapGet("/api/telemetry/queue", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(new { standard = telemetryQueue.PeekAll(), critical = criticalQueue.PeekAll() });
});

app.MapGet("/api/telemetry/timeline", (HttpRequest r, string? mac, int take = 50) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(string.IsNullOrWhiteSpace(mac) ? timeline.Latest(take) : timeline.ForDevice(mac, take));
});

app.MapPost("/api/telemetry/process-next", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();

    // Priority first, then FIFO
    if (criticalQueue.TryDequeue(out var crit))
        return Results.Ok(new { processed = "CRITICAL", packet = crit });

    if (telemetryQueue.TryDequeue(out var normal))
        return Results.Ok(new { processed = "STANDARD", packet = normal });

    return Results.Ok(new { processed = "NONE", packet = (object?)null });
});

// ---------- COMMANDS + UNDO STACK ----------
app.MapPost("/api/commands", (HttpRequest r, [FromBody] CommandRequest req) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();

    var rec = new CommandRecord
    {
        MacAddress = req.MacAddress,
        DeviceName = devices.TryGet(req.MacAddress, out var d) ? d.Name : req.MacAddress,
        Command = req.Command,
        PreviousState = req.PreviousState
    };
    undoStack.Push(rec);

    // Record event for predictive engine
    predictive.RecordEvent($"command:{req.MacAddress}:{req.Command}");

    return Results.Ok(new { message = $"Command '{req.Command}' queued.", stackDepth = undoStack.Count, record = rec });
});

app.MapPost("/api/commands/undo", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    // Never crashes: TryUndo returns false when empty
    if (!undoStack.TryUndo(out var undone))
        return Results.Ok(new { message = "Nothing to undo.", undone = (object?)null });

    predictive.RecordEvent($"undo:{undone!.MacAddress}");
    return Results.Ok(new { message = $"Undid '{undone.Command}' on {undone.DeviceName}.", undone });
});

app.MapGet("/api/commands/history", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(undoStack.History());
});

// ---------- ALERTS (HashSet uniqueness) ----------
app.MapGet("/api/alerts/active", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(disconnected.All());
});

app.MapPost("/api/alerts/clear", (HttpRequest r, string mac, string reason) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(new { cleared = disconnected.Clear(mac, reason) });
});

// ---------- SEARCH + PREDICTIVE ENGINE ----------
app.MapPost("/api/search", (HttpRequest r, [FromBody] SearchEventRequest req) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    predictive.RecordEvent($"search:{req.Term}");
    if (!string.IsNullOrWhiteSpace(req.Category))
        predictive.RecordEvent($"category:{req.Category}");

    var suggestions = predictive.GetSuggestions($"search:{req.Term}");
    return Results.Ok(new { suggestions });
});

app.MapGet("/api/insights", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(predictive.HighlightProblemDevices(devices.All));
});

// ---------- ISSUES (kept from Part 1) ----------
var issues = new List<string>();
app.MapGet("/api/issues", (HttpRequest r) => IsAuthed(r) ? Results.Ok(issues) : Results.Unauthorized());
app.MapPost("/api/issues", (HttpRequest r, [FromBody] IssueRequest req) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    issues.Add($"[{DateTime.UtcNow:HH:mm:ss}] {req.Description}");
    return Results.Ok(new { message = "Issue logged." });
});

app.Run();