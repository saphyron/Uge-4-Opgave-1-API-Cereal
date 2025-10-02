using CerealAPI.Endpoints.CRUD;
using CerealAPI.Endpoints.Ops;
using CerealAPI.Endpoints.Authentication;
using CerealAPI.Endpoints.Products;
using CerealAPI.Utils.Security;              
using CerealAPI.Data;                  
using CerealAPI.Data.Repository;                  
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// JWT konfiguration (cookie-først + header fallback)
var jwtKey      = Authz.GetJwtSecret(builder.Configuration);
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? builder.Configuration["JWT_ISSUER"];
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["JWT_AUDIENCE"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
            ValidIssuer = jwtIssuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true
        };

        // Læs token fra Authorization-header (Bearer) eller cookie 'token'
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("JwtDebug");
                // Log alle cookies
                var allCookies = string.Join(", ", ctx.Request.Cookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                logger?.LogInformation($"JWT: Cookies received: {allCookies}");

                var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Token = authHeader.Substring("Bearer ".Length).Trim();
                    logger?.LogInformation("JWT: Token found in Authorization header.");
                }
                else if (ctx.Request.Cookies.TryGetValue("token", out var t) && !string.IsNullOrWhiteSpace(t))
                {
                    ctx.Token = t;
                    logger?.LogInformation("JWT: Token found in cookie.");
                }
                else
                {
                    logger?.LogWarning("JWT: No token found in header or cookie.");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("WriteOps", p => p.RequireAuthenticatedUser());
});

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// DI: DB + Repositories
builder.Services.AddSingleton(new CerealAPI.Data.SqlConnectionCeral(
    builder.Configuration.GetConnectionString("Default")!
));
builder.Services.AddScoped<UsersRepository>();

var app = builder.Build();

// Swagger/OpenAPI i Development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestLoggingMiddleware>();

// Endpoints
app.MapOpsEndpoints();
app.MapCrudEndpoints();              // husk at beskytte POST/PUT/DELETE med .RequireAuthorization("WriteOps")
app.MapAuthenticationEndpoints();    // /auth register/login/me/logout
app.MapProductsEndpoints();          // beskyt mutationer med .RequireAuthorization("WriteOps")

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
