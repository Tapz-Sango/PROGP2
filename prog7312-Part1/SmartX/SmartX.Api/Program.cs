using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

// In-Memory Data Stores
var registeredSensors = new List<SensorRecord>
{
    new SensorRecord("A4:C1:38:9B:4F:10", "Hydroponic Zone B", "Environmental", DateTime.UtcNow),
    new SensorRecord("C8:2B:96:11:02:AA", "Utility Grid Meter 1", "Power Consumption", DateTime.UtcNow)
};

var reportedIssues = new List<string>();

// --- ENDPOINTS ---

// 1. Authentication Endpoint
app.MapPost("/api/auth/login", ([FromBody] AuthRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { message = "Email and Password are required." });
    }

    if (req.Email == "guest@smartx.io" || req.Password == "admin123")
    {
        return Results.Ok(new AuthResponse(Guid.NewGuid().ToString(), req.Email, "Bearer-Token-SmartX-Authenticated"));
    }

    return Results.Unauthorized();
});

// 2. Sensor Management Endpoints
app.MapGet("/api/sensors", () => Results.Ok(registeredSensors));

app.MapPost("/api/sensors", ([FromBody] SensorRecord sensor) =>
{
    if (string.IsNullOrEmpty(sensor.MacAddress) || !sensor.MacAddress.Contains(":"))
    {
        return Results.BadRequest(new { message = "Invalid MAC Address syntax." });
    }

    registeredSensors.Add(sensor);
    return Results.Created($"/api/sensors/{sensor.MacAddress}", sensor);
});

// 3. Encrypted File Stream Endpoint
app.MapPost("/api/sensors/{mac}/upload", async (string mac, IFormFile file) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No file stream payload provided." });
    }

    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
    if (!Directory.Exists(uploadsFolder))
    {
        Directory.CreateDirectory(uploadsFolder);
    }

    // Windows allows the MAC address in the file path
    string safeMac = mac.Replace(":", "-");
    var encryptedFilePath = Path.Combine(uploadsFolder, $"{safeMac}_{Guid.NewGuid()}_{file.FileName}.enc");

    using (Aes aes = Aes.Create())
    {
        // Exactly 32 bytes for AES-256 key
        aes.Key = Encoding.UTF8.GetBytes("SmartXKey32BytesLongSecret!!1234");
        aes.IV = new byte[16]; // 16 bytes for AES block size

        using FileStream fs = new FileStream(encryptedFilePath, FileMode.Create);
        using CryptoStream cs = new CryptoStream(fs, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await file.CopyToAsync(cs);
    }

    return Results.Ok(new { status = "Encrypted & Stored", fileName = file.FileName, path = encryptedFilePath });
})
.DisableAntiforgery(); // Disables antiforgery check for desktop WPF HTTP requests

// 4. Anomaly Log Endpoints
app.MapGet("/api/issues", () => Results.Ok(reportedIssues));

app.MapPost("/api/issues", ([FromBody] IssueModel issue) =>
{
    reportedIssues.Add($"[{DateTime.UtcNow:HH:mm:ss}] {issue.Description}");
    return Results.Ok(new { message = "Issue logged successfully." });
});

app.Run();

// --- DATA MODELS / RECORDS ---
public record AuthRequest(string Email, string Password);
public record AuthResponse(string UserId, string Email, string Token);
public record SensorRecord(string MacAddress, string Location, string Category, DateTime RegisteredAt);
public record IssueModel(string Description);