using AuraVault.Core.Cryptography;

namespace AuraVault.Core.Kdbx;

/// <summary>Translates between <see cref="KdfParameters"/> and the KDBX 4 VariantDictionary form.</summary>
internal static class KdbxKdf
{
    private const string KeyUuid = "$UUID";

    // Argon2 keys
    private const string ArgonSalt = "S";
    private const string ArgonParallelism = "P";
    private const string ArgonMemory = "M";
    private const string ArgonIterations = "I";
    private const string ArgonVersion = "V";

    // AES-KDF keys
    private const string AesRounds = "R";
    private const string AesSeed = "S";

    public static KdfParameters FromVariantDictionary(VariantDictionary dict)
    {
        byte[] uuid = dict.TryGetByteArray(KeyUuid)
            ?? throw new KdbxFormatException("KDF parameters have no $UUID.");

        if (uuid.AsSpan().SequenceEqual(KdfUuids.Argon2id) || uuid.AsSpan().SequenceEqual(KdfUuids.Argon2d))
        {
            bool isId = uuid.AsSpan().SequenceEqual(KdfUuids.Argon2id);
            return new Argon2KdfParameters(
                Salt: dict.GetByteArray(ArgonSalt),
                Parallelism: dict.GetUInt32(ArgonParallelism),
                MemoryBytes: dict.GetUInt64(ArgonMemory),
                Iterations: dict.GetUInt64(ArgonIterations),
                Version: dict.GetUInt32(ArgonVersion),
                IsArgon2id: isId);
        }

        if (uuid.AsSpan().SequenceEqual(KdfUuids.Aes))
        {
            return new AesKdfParameters(
                Seed: dict.GetByteArray(AesSeed),
                Rounds: dict.GetUInt64(AesRounds));
        }

        throw new KdbxFormatException("Unsupported KDF UUID in KDF parameters.");
    }

    public static VariantDictionary ToVariantDictionary(KdfParameters parameters)
    {
        var dict = new VariantDictionary();
        dict.SetByteArray(KeyUuid, parameters.Uuid);

        switch (parameters)
        {
            case Argon2KdfParameters a:
                dict.SetByteArray(ArgonSalt, a.Salt);
                dict.SetUInt32(ArgonParallelism, a.Parallelism);
                dict.SetUInt64(ArgonMemory, a.MemoryBytes);
                dict.SetUInt64(ArgonIterations, a.Iterations);
                dict.SetUInt32(ArgonVersion, a.Version);
                break;

            case AesKdfParameters aes:
                dict.SetByteArray(AesSeed, aes.Seed);
                dict.SetUInt64(AesRounds, aes.Rounds);
                break;

            default:
                throw new NotSupportedException($"Cannot serialize KDF parameters of type {parameters.GetType().Name}.");
        }

        return dict;
    }

    /// <summary>Returns a copy of <paramref name="parameters"/> with a freshly generated salt/seed.</summary>
    public static KdfParameters WithFreshSalt(KdfParameters parameters) => parameters switch
    {
        Argon2KdfParameters a => a with { Salt = CryptoRandom.GetBytes(a.Salt.Length == 0 ? 32 : a.Salt.Length) },
        AesKdfParameters aes => aes with { Seed = CryptoRandom.GetBytes(32) },
        _ => parameters,
    };
}
