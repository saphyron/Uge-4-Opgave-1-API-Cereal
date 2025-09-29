namespace CerealAPI.Endpoints.Authentication
{
    public static class AuthenticationEndpoints
    {
        public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
        {
            // Stub/placeholder – tilpas efter behov
            app.MapGet("/auth/health", () => Results.Ok(new { ok = true }));
            return app;
        }
    }
}
