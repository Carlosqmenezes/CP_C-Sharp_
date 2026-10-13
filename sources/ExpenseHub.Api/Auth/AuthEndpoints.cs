using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Auth;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/login", async (LoginRequest request, AuthService auth) => Results.Ok(await auth.LoginAsync(request)))
            .AllowAnonymous();

        app.MapPost("/register", async (RegisterRequest request, RegistrationService registration) =>
        {
            RegisterResponse response = await registration.RegisterAsync(request);
            return Results.Created($"/api/admin/users/{response.Id}", response);
        })
            .AllowAnonymous();

        return app;
    }
}
