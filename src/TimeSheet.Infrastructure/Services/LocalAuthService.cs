using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class LocalAuthService(IUserRepository users, IPasswordHasher passwordHasher, IConfiguration configuration) : ILocalAuthService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);

    public async Task<LocalLoginResult?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive || user.PasswordHash is null) return null;
        if (!passwordHasher.Verify(password, user.PasswordHash)) return null;

        var signingKey = configuration["LocalAuth:JwtSigningKey"]
            ?? throw new InvalidOperationException("Missing LocalAuth:JwtSigningKey configuration.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);

        var expires = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var token = new JwtSecurityToken(
            issuer: LocalAuthConstants.Issuer,
            audience: LocalAuthConstants.Audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email)],
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new LocalLoginResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
