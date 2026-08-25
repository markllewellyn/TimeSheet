using System.Security.Cryptography;

namespace TimeSheet.Infrastructure.Services;

/// <summary>Shared by GraphAdminUserService (Entra accounts) and LocalUserPasswordService (local accounts) -
/// both need a one-time temporary password to hand back to the Admin.</summary>
public static class TemporaryPasswordGenerator
{
    public static string Generate()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        const string all = upper + lower + digits + symbols;

        // Guarantee at least one of each character class, then fill the rest randomly - meets typical
        // password complexity policies (upper/lower/digit/symbol, 12+ characters). Shuffled (not just
        // appended) so the guaranteed characters aren't always in the same predictable positions.
        var chars = new[]
        {
            PickRandom(upper), PickRandom(lower), PickRandom(digits), PickRandom(symbols),
        }.Concat(Enumerable.Range(0, 8).Select(_ => PickRandom(all))).ToArray();

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char PickRandom(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
