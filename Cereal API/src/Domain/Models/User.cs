namespace CerealAPI.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Role { get; set; } = "user";
    public DateTime CreatedAt { get; set; }
}

public sealed class RegisterRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public sealed class LoginRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public sealed class AuthResponse
{
    public string Token { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Role { get; set; } = default!;
}
