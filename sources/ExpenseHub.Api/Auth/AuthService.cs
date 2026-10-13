using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Auth;

internal sealed class AuthService(UserManager<ApplicationUser> userManager, TokenService tokens)
{
    private const string InvalidCredentials = "E-mail ou senha inválidos.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        RequestValidator.Validate(request);

        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        IList<string> roles = await userManager.GetRolesAsync(user);
        return tokens.CreateToken(user, roles);
    }
}
