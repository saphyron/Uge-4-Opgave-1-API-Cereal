// src/Endpoints/Authentication/AuthenticationEndpoints.cs
using CerealAPI.Data.Repository;
using CerealAPI.Utils.Security;
using Microsoft.AspNetCore.Authorization;

namespace CerealAPI.Endpoints.Authentication;

public static class AuthenticationEndpoints
{
    // Vi bruger WebApplication, så vi kan tjekke app.Environment.IsDevelopment()
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/auth");

        // ÅBNE endpoints
        g.MapGet("/health", () => Results.Ok(new { ok = true })).AllowAnonymous();

        g.MapPost("/register", async (UsersRepository repo, RegisterRequest req) =>
        {
            if (req is null ||
                string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { message = "Username and password are required." });
            }

            var exists = await repo.GetByUsernameAsync(req.Username);
            if (exists is not null)
                return Results.Conflict(new { message = "Username already exists." });

            var hash = PasswordHasher.Hash(req.Password);
            var newId = await repo.CreateAsync(req.Username, hash, role: "user");
            return Results.Created($"/users/{newId}", new { id = newId, username = req.Username });
        }).AllowAnonymous();

        g.MapPost("/login", async (UsersRepository repo, IConfiguration cfg, HttpContext ctx, LoginRequest req) =>
        {
            if (req is null ||
                string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.Unauthorized();
            }

            var user = await repo.GetByUsernameAsync(req.Username);
            if (user is null) return Results.Unauthorized();

            var ok = PasswordHasher.LooksHashed(user.PasswordHash)
                ? PasswordHasher.Verify(req.Password, user.PasswordHash)
                : string.Equals(req.Password, user.PasswordHash, StringComparison.Ordinal);

            if (!ok) return Results.Unauthorized();

            if (!PasswordHasher.LooksHashed(user.PasswordHash))
            {
                var newHash = PasswordHasher.Hash(req.Password);
                await repo.UpdatePasswordHashAsync(user.Id, newHash);
            }

            var secret = Authz.GetJwtSecret(cfg);
            var kid    = cfg["JWT_KEY_VERSION"] ?? "v1";
            var iss    = cfg["Jwt:Issuer"]   ?? cfg["JWT_ISSUER"];
            var aud    = cfg["Jwt:Audience"] ?? cfg["JWT_AUDIENCE"];

            var jwt = JwtHelper.CreateAccessToken(
                new JwtHelper.JwtUser { UserId = user.Id, Username = user.Username, Role = user.Role },
                secret, kid, lifetime: TimeSpan.FromHours(8), issuer: iss, audience: aud
            );

            //var secure = false; // dev over HTTP => false
            var secure = ctx.Request.IsHttps && !app.Environment.IsDevelopment();
            //var secure = ctx.Request.IsHttps; // dev over HTTP => false
            var now    = DateTimeOffset.UtcNow;

            ctx.Response.Cookies.Append("token", jwt, new CookieOptions
            {
                HttpOnly = true,
                Secure   = ctx.Request.IsHttps, 
                SameSite = SameSiteMode.Lax,
                Path     = "/",
                MaxAge   = TimeSpan.FromHours(8),
                Expires  = now.AddHours(8),
                IsEssential = true
            });

            return Results.Ok(new AuthResponse { Token = jwt, Username = user.Username, Role = user.Role });
        }).AllowAnonymous();

        g.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Append("token", "", new CookieOptions
            {
                HttpOnly = true,
                Secure   = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path     = "/",
                Expires  = DateTime.UnixEpoch
            });
            return Results.Ok(new { success = true });
        }).AllowAnonymous();

        // BESKYTTET endpoint via middleware – kraver blot login
        g.MapGet("/me", async (HttpContext ctx, UsersRepository repo) =>
        {
            if (ctx.User?.Identity is not { IsAuthenticated: true })
                return Results.Unauthorized();

            var username = ctx.User.Identity!.Name; // ClaimTypes.Name fra JWT
            if (string.IsNullOrWhiteSpace(username))
                return Results.Unauthorized();

            var user = await repo.GetByUsernameAsync(username);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new { user = new { user.Id, user.Username, user.Role } });
        }).RequireAuthorization();

        // (valgfrit) dev-echo for at se om cookie ankommer
        if (app.Environment.IsDevelopment())
        {
            g.MapGet("/_echo", (HttpContext ctx) =>
            {
                var hasTokenCookie = ctx.Request.Cookies.ContainsKey("token");
                var bearer = ctx.Request.Headers.Authorization.ToString();
                return Results.Ok(new
                {
                    hasTokenCookie,
                    bearerStartsWith = bearer.StartsWith("Bearer ") ? "Bearer ..." : "(none)"
                });
            }).AllowAnonymous();
        }
    }
}

// DTOs (læg dem evt. i egen fil hvis du foretrækker)
public sealed record RegisterRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);

public sealed class AuthResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "user";
}
