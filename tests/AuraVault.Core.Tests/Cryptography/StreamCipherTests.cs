using AuraVault.Core.Cryptography;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Cryptography;

public sealed class StreamCipherTests
{
    // RFC 8439 §2.3.2 — keystream block for counter = 1 with the sample key/nonce.
    // BouncyCastle's ChaCha7539Engine starts at counter 0, so we skip the first 64 bytes.
    [Fact]
    public void ChaCha20_matches_rfc8439_keystream_block_for_counter_1()
    {
        byte[] key =
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f,
        ];
        byte[] nonce = [0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x4a, 0x00, 0x00, 0x00, 0x00];

        byte[] expectedBlock1 =
        [
            0x10, 0xf1, 0xe7, 0xe4, 0xd1, 0x3b, 0x59, 0x15, 0x50, 0x0f, 0xdd, 0x1f, 0xa3, 0x20, 0x71, 0xc4,
            0xc7, 0xd1, 0xf4, 0xc7, 0x33, 0xc0, 0x68, 0x03, 0x04, 0x22, 0xaa, 0x9a, 0xc3, 0xd4, 0x6c, 0x4e,
            0xd2, 0x82, 0x64, 0x46, 0x07, 0x9f, 0xaa, 0x09, 0x14, 0xc2, 0xd7, 0x05, 0xd9, 0x8b, 0x02, 0xa2,
            0xb5, 0x12, 0x9c, 0xd1, 0xde, 0x16, 0x4e, 0xb9, 0xcb, 0xd0, 0x83, 0xe8, 0xa2, 0x50, 0x3c, 0x4e,
        ];

        using var engine = new ChaCha20Engine(key, nonce);
        byte[] keystream = new byte[128];
        engine.NextKeyStream(keystream);

        keystream.AsSpan(64, 64).ToArray().Should().Equal(expectedBlock1);
    }

    [Fact]
    public void ChaCha20_is_xor_symmetric()
    {
        byte[] key = CryptoRandom.GetBytes(32);
        byte[] nonce = CryptoRandom.GetBytes(12);
        byte[] plaintext = CryptoRandom.GetBytes(4096 + 7);

        byte[] ciphertext = new byte[plaintext.Length];
        using (var enc = new ChaCha20Engine(key, nonce))
        {
            enc.Process(plaintext, ciphertext);
        }

        byte[] roundTrip = new byte[plaintext.Length];
        using (var dec = new ChaCha20Engine(key, nonce))
        {
            dec.Process(ciphertext, roundTrip);
        }

        roundTrip.Should().Equal(plaintext);
        ciphertext.Should().NotEqual(plaintext);
    }

    [Fact]
    public void Salsa20_keystream_is_deterministic_for_a_key()
    {
        byte[] key = CryptoRandom.GetBytes(32);

        byte[] a = new byte[64];
        byte[] b = new byte[64];
        using (var e1 = new Salsa20Engine(key, Salsa20Engine.KeePassInnerNonce))
        {
            e1.NextKeyStream(a);
        }

        using (var e2 = new Salsa20Engine(key, Salsa20Engine.KeePassInnerNonce))
        {
            e2.NextKeyStream(b);
        }

        a.Should().Equal(b);
        a.Should().NotEqual(new byte[64]);
    }
}
