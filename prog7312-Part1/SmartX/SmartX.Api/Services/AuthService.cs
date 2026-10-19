using System.Security.Cryptography;

namespace SmartX.Api.Services;

// Proper token-based auth — fixes Part 1 feedback ("require both email AND password,
// make sensitive endpoints check the token").
// In-memory store mimics Firebase-style behaviour for the marker.
public class AuthService
{
    // Seed demo user. In a real system this would be Firebase/Identity.
    private readonly Dictionary<string, string> _users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["guest@smartx.io"] = "admin123",
        ["operator@smartx.io"] = "SmartX@2026"
    };

    private readonly Dictionary<string, (string Email, DateTime ExpiresUtc)> _tokens = new();

    public bool ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return false;
        return _users.TryGetValue(email, out var stored) && stored == password;
    }

    public string IssueToken(string email)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _tokens[token] = (email, DateTime.UtcNow.AddHours(4));
        return token;
    }

    public bool ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (!_tokens.TryGetValue(token, out var entry)) return false;
        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            _tokens.Remove(token);
            return false;
        }
        return true;
    }

    public void Revoke(string token) => _tokens.Remove(token);
}