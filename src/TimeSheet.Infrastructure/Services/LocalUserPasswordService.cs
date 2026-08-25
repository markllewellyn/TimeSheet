using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class LocalUserPasswordService(IUserRepository users, IPasswordHasher passwordHasher, IUnitOfWork uow) : ILocalUserPasswordService
{
    public async Task<string> ResetPasswordAsync(int userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (!user.IsLocalAccount)
        {
            throw new InvalidOperationException("This is an SSO account - reset it through Entra ID instead.");
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.PasswordHash = passwordHasher.Hash(temporaryPassword);
        user.ModifiedUtc = DateTimeOffset.UtcNow;
        users.Update(user);
        await uow.SaveChangesAsync(ct);

        return temporaryPassword;
    }

    public (string PasswordHash, string TemporaryPassword) GenerateInitialCredentials()
    {
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        return (passwordHasher.Hash(temporaryPassword), temporaryPassword);
    }
}
