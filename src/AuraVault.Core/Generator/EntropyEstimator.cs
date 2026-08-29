namespace AuraVault.Core.Generator;

public enum PasswordStrength
{
    VeryWeak,
    Weak,
    Reasonable,
    Strong,
    VeryStrong,
}

/// <summary>
/// A pragmatic strength estimate. For generated secrets it reports the true generation entropy; for
/// user-typed values it uses an observed-pool estimate with light penalties for obvious patterns.
/// </summary>
public static class EntropyEstimator
{
    /// <summary>Exact entropy of a diceware passphrase: <c>words · log2(listSize) + digits · log2(10)</c>.</summary>
    public static double PassphraseBits(int wordCount, int listSize, int appendedDigits = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wordCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(listSize, 2);
        return (wordCount * Math.Log2(listSize)) + (appendedDigits * Math.Log2(10));
    }

    /// <summary>Observed-pool entropy for an arbitrary string, minus small penalties for runs/repeats.</summary>
    public static double PoolBits(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
        {
            return 0;
        }

        int pool = 0;
        if (password.Any(char.IsAsciiDigit))
        {
            pool += 10;
        }

        if (password.Any(char.IsAsciiLetterLower))
        {
            pool += 26;
        }

        if (password.Any(char.IsAsciiLetterUpper))
        {
            pool += 26;
        }

        if (password.Any(c => !char.IsAsciiLetterOrDigit(c) && c <= 0x7E && c > 0x20))
        {
            pool += 33;
        }

        if (password.Any(c => c > 0x7E))
        {
            pool += 100; // rough allowance for non-ASCII
        }

        pool = Math.Max(pool, 2);
        double raw = password.Length * Math.Log2(pool);

        // Penalties: repeated characters and simple ascending/descending runs.
        int repeats = 0;
        int runs = 0;
        for (int i = 1; i < password.Length; i++)
        {
            if (password[i] == password[i - 1])
            {
                repeats++;
            }

            int delta = password[i] - password[i - 1];
            if (delta is 1 or -1)
            {
                runs++;
            }
        }

        double penalty = (repeats * 1.5) + (runs * 1.0);
        return Math.Max(0, raw - penalty);
    }

    public static PasswordStrength Classify(double bits) => bits switch
    {
        < 28 => PasswordStrength.VeryWeak,
        < 36 => PasswordStrength.Weak,
        < 60 => PasswordStrength.Reasonable,
        < 128 => PasswordStrength.Strong,
        _ => PasswordStrength.VeryStrong,
    };
}
