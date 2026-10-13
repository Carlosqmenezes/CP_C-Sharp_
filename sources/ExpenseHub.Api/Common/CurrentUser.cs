using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ExpenseHub.Api.Common;

internal sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirst(ClaimNames.Subject)?.Value;

    public bool IsInRole(string role) => Principal?.HasClaim(ClaimNames.Role, role) ?? false;
}
