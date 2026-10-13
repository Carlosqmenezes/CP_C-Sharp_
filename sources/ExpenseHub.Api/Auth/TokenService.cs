using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseHub.Api.Auth;

internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public LoginResponse CreateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        JwtOptions jwt = options.Value;
        DateTime now = clock.GetUtcNow().UtcDateTime;
        DateTime expires = now.AddMinutes(jwt.ExpirationMinutes);

        List<Claim> claims =
        [
            new Claim(ClaimNames.Subject, user.Id),
            new Claim(ClaimNames.Email, user.Email ?? string.Empty),
        ];
        claims.AddRange(roles.Select(role => new Claim(ClaimNames.Role, role)));

        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return new LoginResponse(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
