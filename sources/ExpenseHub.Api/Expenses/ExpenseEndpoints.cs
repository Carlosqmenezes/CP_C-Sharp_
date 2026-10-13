using System;
using System.Threading;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Expenses;

internal static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/expenses");

        group.MapPost(string.Empty, async (ExpenseRequest request, ExpenseService expenses, CancellationToken ct) =>
        {
            ExpenseResponse created = await expenses.CreateAsync(request, ct);
            return Results.Created($"/api/expenses/{created.Id}", created);
        })
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Employee });

        group.MapPut("/{id:guid}", async (Guid id, ExpenseRequest request, ExpenseService expenses, CancellationToken ct) =>
                Results.Ok(await expenses.UpdateAsync(id, request, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Employee });

        group.MapGet(string.Empty, async (ExpenseService expenses, CancellationToken ct) =>
                Results.Ok(await expenses.ListAsync(ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.ExpenseReaders });

        group.MapGet("/{id:guid}", async (Guid id, ExpenseService expenses, CancellationToken ct) =>
                Results.Ok(await expenses.GetAsync(id, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.ExpenseReaders });

        group.MapPost("/{id:guid}/submit", async (Guid id, ExpenseService expenses, CancellationToken ct) =>
                Results.Ok(await expenses.SubmitAsync(id, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Employee });

        return app;
    }
}
