using System.Security.Cryptography;

namespace AuraVault.Core.Cryptography;

/// <summary>Thin, testable facade over <see cref="RandomNumberGenerator"/>.</summary>
public static class CryptoRandom
{
    public static byte[] GetBytes(int count)
    {
        var buffer = new byte[count];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    public static void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);

    /// <summary>Uniform integer in [0, exclusiveMax) without modulo bias.</summary>
    public static int Next(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        return RandomNumberGenerator.GetInt32(exclusiveMax);
    }
}
