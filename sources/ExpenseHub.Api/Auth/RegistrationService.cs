using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Auth;

internal sealed class RegistrationService(UserManager<ApplicationUser> userManager)
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        RequestValidator.Validate(request);

        ApplicationUser user = new() { UserName = request.Email, Email = request.Email };
        IdentityResult result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new BadRequestException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return new RegisterResponse(user.Id, request.Email);
    }
}
