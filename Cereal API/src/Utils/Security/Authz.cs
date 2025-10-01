// CerealAPI/src/Utils/Security/Authz.cs
using CerealAPI.Data;
using Dapper;

namespace CerealAPI.Utils.Security;

public static class Authz
{
    // Tillad både "Jwt:SigningKey" (fra tidligere forslag) og fallback "JWT_SECRET"
    public static string GetJwtSecret(IConfiguration cfg)
        => cfg["Jwt:SigningKey"]
        ?? cfg["JWT_SECRET"]
        ?? "dev-only-secret-change-me";

    public static string? TryGetToken(HttpContext ctx)
    {
        // 1) Cookie (HttpOnly)
        if (ctx.Request.Cookies.TryGetValue("token", out var cookieToken) &&
            !string.IsNullOrEmpty(cookieToken))
            return cookieToken;

        // 2) Fallback: Authorization: Bearer ...
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth) &&
            auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth.Substring("Bearer ".Length).Trim();

        return null;
    }

    /// <summary>
    /// Valider token og slå brugeren op i DB (sikrer at bruger/rolle stadig findes).
    /// Returnerer null hvis ikke gyldig.
    /// </summary>
    public static async Task<JwtHelper.JwtUser?> GetCurrentUserAsync(
        SqlConnectionCeral factory,
        HttpContext ctx,
        IConfiguration cfg)
    {
        var token  = TryGetToken(ctx);
        if (string.IsNullOrEmpty(token)) return null;

        var issuer   = cfg["Jwt:Issuer"]   ?? cfg["JWT_ISSUER"];
        var audience = cfg["Jwt:Audience"] ?? cfg["JWT_AUDIENCE"];
        var secret   = GetJwtSecret(cfg);

        var u = JwtHelper.Validate(token, secret, issuer, audience);
        if (u is null) return null;

        using var conn = factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<(int Id, string Username, string Role)>(
            "SELECT Id, Username, Role FROM dbo.Users WHERE Username=@u",
            new { u = u.Username });

        if (row.Id == 0) return null;

        // returnér claims (vi holder skema simpelt i dette projekt)
        return new JwtHelper.JwtUser
        {
            UserId = row.Id,
            Username = row.Username,
            Role = row.Role
        };
    }
}
