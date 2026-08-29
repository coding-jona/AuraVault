namespace AuraVault.Core.Kdbx;

/// <summary>Wire-format constants for KDBX 3.1 / 4.x.</summary>
public static class KdbxFormat
{
    public const uint Signature1 = 0x9AA2D903;
    public const uint Signature2 = 0xB54BFB67;

    public const uint FileVersionCriticalMask = 0xFFFF0000;
    public const uint FileVersion4 = 0x00040000;
    public const uint FileVersion41 = 0x00040001;
    public const uint FileVersion31 = 0x00030001;

    /// <summary>Outer (pre-encryption) header field identifiers.</summary>
    public enum HeaderField : byte
    {
        EndOfHeader = 0,
        Comment = 1,
        CipherId = 2,
        CompressionFlags = 3,
        MasterSeed = 4,
        TransformSeed = 5,        // KDBX 3.1 only
        TransformRounds = 6,      // KDBX 3.1 only
        EncryptionIv = 7,
        InnerRandomStreamKey = 8, // KDBX 3.1 only (moves to inner header in v4)
        StreamStartBytes = 9,     // KDBX 3.1 only
        InnerRandomStreamId = 10, // KDBX 3.1 only (moves to inner header in v4)
        KdfParameters = 11,       // KDBX 4 only
        PublicCustomData = 12,    // KDBX 4 only
    }

    /// <summary>Inner (post-decompression) header field identifiers — KDBX 4 only.</summary>
    public enum InnerHeaderField : byte
    {
        EndOfHeader = 0,
        InnerRandomStreamId = 1,
        InnerRandomStreamKey = 2,
        Binary = 3,
    }

    public enum CompressionAlgorithm : uint
    {
        None = 0,
        GZip = 1,
    }

    public enum InnerRandomStreamId : uint
    {
        None = 0,
        ArcFourVariant = 1, // obsolete, unsupported
        Salsa20 = 2,
        ChaCha20 = 3,
    }

    /// <summary>Cipher UUIDs as stored in <see cref="HeaderField.CipherId"/>.</summary>
    public static class CipherUuids
    {
        public static ReadOnlySpan<byte> Aes256Cbc => [0x31, 0xC1, 0xF2, 0xE6, 0xBF, 0x71, 0x43, 0x50, 0xBE, 0x58, 0x05, 0x21, 0x6A, 0xFC, 0x5A, 0xFF];
        public static ReadOnlySpan<byte> ChaCha20 => [0xD6, 0x03, 0x8A, 0x2B, 0x8B, 0x6F, 0x4C, 0xB5, 0xA5, 0x24, 0x33, 0x9A, 0x31, 0xDB, 0xB5, 0x9A];
    }

    /// <summary>KeePass binary attachment flag bits (inner header field 3, first byte).</summary>
    [Flags]
    public enum BinaryFlags : byte
    {
        None = 0,
        MemoryProtected = 1,
    }
}
