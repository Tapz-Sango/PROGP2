namespace SmartX.Api.Models;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string UserId, string Email, string Token, DateTime ExpiresUtc);
public record IssueRequest(string Description);
public record SearchEventRequest(string Term, string Category);
public record CommandRequest(string MacAddress, string Command, string PreviousState);
public record TelemetryIngestRequest(string MacAddress, double Value, string Unit, bool IsCritical);