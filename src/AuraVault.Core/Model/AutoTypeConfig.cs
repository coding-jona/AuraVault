namespace AuraVault.Core.Model;

/// <summary>A window-title pattern paired with the auto-type key sequence to send to it.</summary>
public sealed class AutoTypeAssociation
{
    public string Window { get; set; } = string.Empty;

    public string KeystrokeSequence { get; set; } = string.Empty;
}

/// <summary>Per-entry auto-type configuration (mirrors KeePass <c>&lt;AutoType&gt;</c>).</summary>
public sealed class AutoTypeConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>KeePass "two-channel auto-type obfuscation" flag (0 = off).</summary>
    public int DataTransferObfuscation { get; set; }

    public string? DefaultSequence { get; set; }

    public List<AutoTypeAssociation> Associations { get; } = [];

    public AutoTypeConfig Clone()
    {
        var copy = new AutoTypeConfig
        {
            Enabled = Enabled,
            DataTransferObfuscation = DataTransferObfuscation,
            DefaultSequence = DefaultSequence,
        };
        foreach (var a in Associations)
        {
            copy.Associations.Add(new AutoTypeAssociation { Window = a.Window, KeystrokeSequence = a.KeystrokeSequence });
        }

        return copy;
    }
}
