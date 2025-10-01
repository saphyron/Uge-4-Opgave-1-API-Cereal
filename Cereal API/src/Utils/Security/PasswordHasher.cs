// CerealAPI/src/Utils/Security/PasswordHasher.cs
using System.Security.Cryptography;
using System.Text;

namespace CerealAPI.Utils.Security;

public static class PasswordHasher
{
    private const int Iterations = 120_000;
    private const int SaltSize   = 16;
    private const int KeySize    = 32;
    private const string Prf     = "HMACSHA256";
    private const string Prefix  = "pbkdf2";

    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Empty password.");

        Span<byte> salt = stackalloc byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt.ToArray(), Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}${Prf}";
    }

    public static bool Verify(string password, string encoded)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encoded)) return false;

            var parts = encoded.Split('$');
            if (parts.Length < 5 || parts[0] != Prefix) return false;

            int iterations = int.Parse(parts[1]);
            byte[] salt    = Convert.FromBase64String(parts[2]);
            byte[] hash    = Convert.FromBase64String(parts[3]);

            var calc = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, hash.Length);
            return CryptographicOperations.FixedTimeEquals(calc, hash);
        }
        catch { return false; }
    }

    public static bool LooksHashed(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.StartsWith($"{Prefix}$", StringComparison.Ordinal);
}
