using System.Security.Cryptography;
using System.Text;

namespace Wanetra.Application.Auth;

public sealed class SetupCodeProvider
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 12;
    private readonly Lock gate = new();
    private string? current;

    public string? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public string Generate()
    {
        var characters = new char[CodeLength];
        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        var code = $"{new string(characters, 0, 4)}-{new string(characters, 4, 4)}-{new string(characters, 8, 4)}";
        lock (gate)
        {
            current = code;
        }

        return code;
    }

    public bool Verify(string? candidate)
    {
        string? expected;
        lock (gate)
        {
            expected = current;
        }

        if (expected is null || candidate is null)
        {
            return false;
        }

        var normalized = Normalize(candidate);
        var normalizedExpected = Normalize(expected);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(normalized),
            Encoding.UTF8.GetBytes(normalizedExpected));
    }

    public void Clear()
    {
        lock (gate)
        {
            current = null;
        }
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
