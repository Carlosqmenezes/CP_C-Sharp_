using System.Threading;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Admin;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/users")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Admin });

        group.MapGet(string.Empty, async (AdminService admin, CancellationToken ct) => Results.Ok(await admin.ListUsersAsync(ct)));

        group.MapPut("/{id}/roles", async (string id, UpdateRolesRequest request, AdminService admin, CancellationToken ct) =>
            Results.Ok(await admin.UpdateRolesAsync(id, request, ct)));

        return app;
    }
}
