using System;
using System.Threading;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Expenses;

internal static class ExpenseWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapExpenseWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/expenses");

        group.MapPost("/{id:guid}/approve", async (Guid id, ExpenseWorkflowService workflow, CancellationToken ct) =>
                Results.Ok(await workflow.ApproveAsync(id, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Approver });

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectExpenseRequest request, ExpenseWorkflowService workflow, CancellationToken ct) =>
                Results.Ok(await workflow.RejectAsync(id, request, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Approver });

        group.MapPost("/{id:guid}/pay", async (Guid id, ExpenseWorkflowService workflow, CancellationToken ct) =>
                Results.Ok(await workflow.PayAsync(id, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Finance });

        group.MapGet("/{id:guid}/history", async (Guid id, ExpenseWorkflowService workflow, CancellationToken ct) =>
                Results.Ok(await workflow.GetHistoryAsync(id, ct)))
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.ExpenseReaders });

        return app;
    }
}
