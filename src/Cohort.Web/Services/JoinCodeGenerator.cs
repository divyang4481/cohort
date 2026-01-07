using System.Security.Cryptography;

namespace Cohort.Web.Services;

public static class JoinCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/1/0 to avoid confusion

    public static string Create(int length = 8)
    {
        if (length < 4 || length > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
