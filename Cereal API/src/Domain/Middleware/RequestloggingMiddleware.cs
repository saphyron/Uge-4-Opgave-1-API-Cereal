// Middleware/RequestLoggingMiddleware.cs
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _log;
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> log)
    { _next = next; _log = log; }

    public async Task Invoke(HttpContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var path = ctx.Request.Path;
        var query = ctx.Request.QueryString.Value ?? "";
        var method = ctx.Request.Method;

        try
        {
            await _next(ctx);
            sw.Stop();
            _log.LogInformation("HTTP {Method} {Path}{Query} -> {Status} in {Elapsed} ms",
                method, path, query, ctx.Response?.StatusCode, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.LogError(ex, "HTTP {Method} {Path}{Query} failed in {Elapsed} ms",
                method, path, query, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
