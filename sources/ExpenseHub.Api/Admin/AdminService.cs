using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Admin;

internal sealed record UserResponse(string Id, string? Email, IReadOnlyList<string> Roles);

internal sealed class UpdateRolesRequest
{
    [Required]
    public IReadOnlyList<string> Roles { get; init; } = [];
}

internal sealed class AdminService(UserManager<ApplicationUser> userManager, AppDbContext db, CurrentUser currentUser)
{
    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken ct = default)
    {
        RequireAdmin();

        var users = await db.Users.AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(ct);

        var assignments = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            select new { userRole.UserId, RoleName = role.Name }).ToListAsync(ct);

        ILookup<string, string> rolesByUser = assignments.ToLookup(item => item.UserId, item => item.RoleName ?? string.Empty);
        return users.Select(user => new UserResponse(user.Id, user.Email, rolesByUser[user.Id].Order().ToList())).ToList();
    }

    public async Task<UserResponse> UpdateRolesAsync(string userId, UpdateRolesRequest request, CancellationToken ct = default)
    {
        RequireAdmin();
        RequestValidator.Validate(request);

        List<string> requested = request.Roles.Select(role => role.Trim()).Distinct().ToList();
        List<string> unknown = requested.Where(role => !Roles.All.Contains(role)).ToList();
        if (unknown.Count > 0)
        {
            throw new BadRequestException($"Roles desconhecidas: {string.Join(", ", unknown)}.");
        }

        ApplicationUser user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("Usuário não encontrado.");

        if (user.Id == currentUser.UserId && !requested.Contains(Roles.Admin))
        {
            throw new BadRequestException("Não é permitido remover a própria role Admin.");
        }

        IList<string> current = await userManager.GetRolesAsync(user);
        List<string> toRemove = current.Except(requested).ToList();
        List<string> toAdd = requested.Except(current).ToList();

        if (toRemove.Count > 0)
        {
            EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, toRemove));
        }

        if (toAdd.Count > 0)
        {
            EnsureSucceeded(await userManager.AddToRolesAsync(user, toAdd));
        }

        return new UserResponse(user.Id, user.Email, requested.Order().ToList());
    }

    private void RequireAdmin()
    {
        if (!currentUser.IsInRole(Roles.Admin))
        {
            throw new ForbiddenException("Apenas Admin pode executar esta operação.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
