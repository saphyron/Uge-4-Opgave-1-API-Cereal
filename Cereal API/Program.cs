using CerealAPI.Endpoints.CRUD;
using CerealAPI.Endpoints.Ops;
using CerealAPI.Endpoints.Authentication;
using CerealAPI.Endpoints.Products;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// DI: SqlConnectionFactory med din connection string
builder.Services.AddSingleton(new CerealAPI.Data.SqlConnectionCeral(
    builder.Configuration.GetConnectionString("Default")!
));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

// Endpoints
app.MapOpsEndpoints();
app.MapCrudEndpoints();
app.MapAuthenticationEndpoints();
app.MapProductsEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
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
