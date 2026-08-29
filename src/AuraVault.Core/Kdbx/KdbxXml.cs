using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AuraVault.Core.Model;

namespace AuraVault.Core.Kdbx;

/// <summary>
/// Reads and writes the KeePass 2.x XML database that sits inside a KDBX payload.
/// Protected field values are XOR-obfuscated with the inner random stream in strict document order.
/// </summary>
internal static class KdbxXml
{
    private static readonly DateTimeOffset KdbxEpoch = new(1, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ---------------------------------------------------------------- READ

    public static Vault Read(ReadOnlySpan<byte> xmlBytes, IInnerRandomStream innerStream, IReadOnlyList<KdbxBinary> binaries)
    {
        XDocument doc;
        using (var ms = new MemoryStream(xmlBytes.ToArray(), writable: false))
        {
            doc = XDocument.Load(ms, LoadOptions.PreserveWhitespace);
        }

        var protectedValues = new HashSet<XElement>();
        foreach (var value in doc.Descendants("Value"))
        {
            if (!IsProtected(value))
            {
                continue;
            }

            protectedValues.Add(value);
            string cipherB64 = value.Value;
            byte[] plain = cipherB64.Length == 0 ? [] : Convert.FromBase64String(cipherB64);
            innerStream.Apply(plain);
            value.Value = Encoding.UTF8.GetString(plain);
            CryptographicOperations.ZeroMemory(plain);
        }

        var file = doc.Root ?? throw new KdbxFormatException("KDBX XML has no root element.");
        if (file.Name != "KeePassFile")
        {
            throw new KdbxFormatException($"Unexpected KDBX XML root <{file.Name}>.");
        }

        var vault = new Vault();
        foreach (var bin in binaries)
        {
            vault.Binaries.Add(bin);
        }

        var metaEl = file.Element("Meta");
        if (metaEl is not null)
        {
            vault.Meta = ReadMeta(metaEl);
        }

        var rootEl = file.Element("Root") ?? throw new KdbxFormatException("KDBX XML has no <Root>.");
        var groupEl = rootEl.Element("Group") ?? throw new KdbxFormatException("KDBX <Root> has no <Group>.");
        vault.Root = ReadGroup(groupEl, protectedValues);

        var deletedEl = rootEl.Element("DeletedObjects");
        if (deletedEl is not null)
        {
            foreach (var d in deletedEl.Elements("DeletedObject"))
            {
                var id = ReadId(d.Element("UUID"));
                var time = ReadTime(d.Element("DeletionTime")) ?? DateTimeOffset.UtcNow;
                vault.DeletedObjects.Add(new DeletedObject(id, time));
            }
        }

        return vault;
    }

    private static VaultMeta ReadMeta(XElement el)
    {
        var meta = new VaultMeta
        {
            Generator = (string?)el.Element("Generator") ?? "AuraVault",
            DatabaseName = (string?)el.Element("DatabaseName") ?? string.Empty,
            DatabaseNameChanged = ReadTime(el.Element("DatabaseNameChanged")),
            DatabaseDescription = (string?)el.Element("DatabaseDescription") ?? string.Empty,
            DatabaseDescriptionChanged = ReadTime(el.Element("DatabaseDescriptionChanged")),
            DefaultUserName = (string?)el.Element("DefaultUserName") ?? string.Empty,
            DefaultUserNameChanged = ReadTime(el.Element("DefaultUserNameChanged")),
            MaintenanceHistoryDays = ReadInt(el.Element("MaintenanceHistoryDays"), 365),
            Color = (string?)el.Element("Color") ?? string.Empty,
            MasterKeyChanged = ReadTime(el.Element("MasterKeyChanged")),
            MasterKeyChangeRec = ReadInt(el.Element("MasterKeyChangeRec"), -1),
            MasterKeyChangeForce = ReadInt(el.Element("MasterKeyChangeForce"), -1),
            RecycleBinEnabled = ReadBool(el.Element("RecycleBinEnabled"), true),
            RecycleBinUuid = ReadId(el.Element("RecycleBinUUID")),
            RecycleBinChanged = ReadTime(el.Element("RecycleBinChanged")),
            EntryTemplatesGroup = ReadId(el.Element("EntryTemplatesGroup")),
            EntryTemplatesGroupChanged = ReadTime(el.Element("EntryTemplatesGroupChanged")),
            HistoryMaxItems = ReadInt(el.Element("HistoryMaxItems"), 10),
            HistoryMaxSize = ReadLong(el.Element("HistoryMaxSize"), 6L * 1024 * 1024),
            SettingsChanged = ReadTime(el.Element("SettingsChanged")),
        };

        var customData = el.Element("CustomData");
        if (customData is not null)
        {
            foreach (var item in customData.Elements("Item"))
            {
                string key = (string?)item.Element("Key") ?? string.Empty;
                if (key.Length != 0)
                {
                    meta.CustomData[key] = (string?)item.Element("Value") ?? string.Empty;
                }
            }
        }

        return meta;
    }

    private static Group ReadGroup(XElement el, HashSet<XElement> protectedValues)
    {
        var group = new Group
        {
            Uuid = ReadId(el.Element("UUID"), generateIfMissing: true),
            Name = (string?)el.Element("Name") ?? string.Empty,
            Notes = (string?)el.Element("Notes") ?? string.Empty,
            IconId = ReadInt(el.Element("IconID"), 48),
            IsExpanded = ReadBool(el.Element("IsExpanded"), true),
            DefaultAutoTypeSequence = (string?)el.Element("DefaultAutoTypeSequence"),
            EnableAutoType = ReadNullableBool(el.Element("EnableAutoType")),
            EnableSearching = ReadNullableBool(el.Element("EnableSearching")),
            Times = ReadTimes(el.Element("Times")),
        };

        var lastTop = el.Element("LastTopVisibleEntry");
        if (lastTop is not null && !string.IsNullOrEmpty(lastTop.Value))
        {
            group.LastTopVisibleEntry = ReadId(lastTop);
        }

        ReadCustomData(el.Element("CustomData"), group.CustomData);

        foreach (var child in el.Elements("Group"))
        {
            group.Groups.Add(ReadGroup(child, protectedValues));
        }

        foreach (var entryEl in el.Elements("Entry"))
        {
            group.Entries.Add(ReadEntry(entryEl, protectedValues));
        }

        return group;
    }

    private static Entry ReadEntry(XElement el, HashSet<XElement> protectedValues)
    {
        var entry = new Entry
        {
            Uuid = ReadId(el.Element("UUID"), generateIfMissing: true),
            IconId = ReadInt(el.Element("IconID"), 0),
            ForegroundColor = NullIfEmpty((string?)el.Element("ForegroundColor")),
            BackgroundColor = NullIfEmpty((string?)el.Element("BackgroundColor")),
            OverrideUrl = NullIfEmpty((string?)el.Element("OverrideURL")),
            QualityCheck = (string?)el.Element("QualityCheck") ?? "True",
            Times = ReadTimes(el.Element("Times")),
        };

        string tags = (string?)el.Element("Tags") ?? string.Empty;
        foreach (var tag in tags.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            entry.Tags.Add(tag);
        }

        foreach (var s in el.Elements("String"))
        {
            string key = (string?)s.Element("Key") ?? string.Empty;
            var valueEl = s.Element("Value");
            if (key.Length == 0 || valueEl is null)
            {
                continue;
            }

            bool isProtected = protectedValues.Contains(valueEl) || IsProtected(valueEl);
            entry.Strings[key] = new ProtectedString(valueEl.Value, isProtected);
        }

        foreach (var b in el.Elements("Binary"))
        {
            string key = (string?)b.Element("Key") ?? string.Empty;
            var valueEl = b.Element("Value");
            var refAttr = valueEl?.Attribute("Ref");
            if (key.Length != 0 && refAttr is not null && int.TryParse(refAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
            {
                entry.Binaries[key] = idx;
            }
        }

        var autoTypeEl = el.Element("AutoType");
        if (autoTypeEl is not null)
        {
            entry.AutoType = ReadAutoType(autoTypeEl);
        }

        ReadCustomData(el.Element("CustomData"), entry.CustomData);

        var historyEl = el.Element("History");
        if (historyEl is not null)
        {
            foreach (var h in historyEl.Elements("Entry"))
            {
                entry.History.Add(ReadEntry(h, protectedValues));
            }
        }

        entry.IsFavorite = entry.Tags.Remove("Favorite") | entry.CustomData.Remove("AuraVault.Favorite");
        return entry;
    }

    private static AutoTypeConfig ReadAutoType(XElement el)
    {
        var config = new AutoTypeConfig
        {
            Enabled = ReadBool(el.Element("Enabled"), true),
            DataTransferObfuscation = ReadInt(el.Element("DataTransferObfuscation"), 0),
            DefaultSequence = NullIfEmpty((string?)el.Element("DefaultSequence")),
        };

        foreach (var assoc in el.Elements("Association"))
        {
            config.Associations.Add(new AutoTypeAssociation
            {
                Window = (string?)assoc.Element("Window") ?? string.Empty,
                KeystrokeSequence = (string?)assoc.Element("KeystrokeSequence") ?? string.Empty,
            });
        }

        return config;
    }

    private static EntryTimes ReadTimes(XElement? el)
    {
        if (el is null)
        {
            return new EntryTimes();
        }

        return new EntryTimes
        {
            CreationTime = ReadTime(el.Element("CreationTime")),
            LastModificationTime = ReadTime(el.Element("LastModificationTime")),
            LastAccessTime = ReadTime(el.Element("LastAccessTime")),
            ExpiryTime = ReadTime(el.Element("ExpiryTime")),
            Expires = ReadBool(el.Element("Expires"), false),
            UsageCount = ReadInt(el.Element("UsageCount"), 0),
            LocationChanged = ReadTime(el.Element("LocationChanged")),
        };
    }

    private static void ReadCustomData(XElement? el, Dictionary<string, string> target)
    {
        if (el is null)
        {
            return;
        }

        foreach (var item in el.Elements("Item"))
        {
            string key = (string?)item.Element("Key") ?? string.Empty;
            if (key.Length != 0)
            {
                target[key] = (string?)item.Element("Value") ?? string.Empty;
            }
        }
    }

    // ---------------------------------------------------------------- WRITE

    public static byte[] Write(Vault vault, IInnerRandomStream innerStream)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
        var file = new XElement("KeePassFile");
        doc.Add(file);

        file.Add(WriteMeta(vault.Meta));

        var root = new XElement("Root");
        file.Add(root);
        root.Add(WriteGroup(vault.Root));

        var deleted = new XElement("DeletedObjects");
        foreach (var d in vault.DeletedObjects)
        {
            deleted.Add(new XElement(
                "DeletedObject",
                new XElement("UUID", d.Uuid.ToBase64()),
                new XElement("DeletionTime", WriteTime(d.DeletionTime))));
        }

        root.Add(deleted);

        // Second pass: obfuscate protected values in document order.
        foreach (var value in doc.Descendants("Value"))
        {
            if (!IsProtected(value))
            {
                continue;
            }

            byte[] plain = Encoding.UTF8.GetBytes(value.Value);
            innerStream.Apply(plain);
            value.Value = Convert.ToBase64String(plain);
            CryptographicOperations.ZeroMemory(plain);
        }

        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
        };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
        }

        return ms.ToArray();
    }

    private static XElement WriteMeta(VaultMeta meta)
    {
        var el = new XElement(
            "Meta",
            new XElement("Generator", meta.Generator),
            new XElement("DatabaseName", meta.DatabaseName),
            new XElement("DatabaseNameChanged", WriteTime(meta.DatabaseNameChanged)),
            new XElement("DatabaseDescription", meta.DatabaseDescription),
            new XElement("DatabaseDescriptionChanged", WriteTime(meta.DatabaseDescriptionChanged)),
            new XElement("DefaultUserName", meta.DefaultUserName),
            new XElement("DefaultUserNameChanged", WriteTime(meta.DefaultUserNameChanged)),
            new XElement("MaintenanceHistoryDays", meta.MaintenanceHistoryDays.ToString(CultureInfo.InvariantCulture)),
            new XElement("Color", meta.Color),
            new XElement("MasterKeyChanged", WriteTime(meta.MasterKeyChanged)),
            new XElement("MasterKeyChangeRec", meta.MasterKeyChangeRec.ToString(CultureInfo.InvariantCulture)),
            new XElement("MasterKeyChangeForce", meta.MasterKeyChangeForce.ToString(CultureInfo.InvariantCulture)),
            new XElement(
                "MemoryProtection",
                new XElement("ProtectTitle", "False"),
                new XElement("ProtectUserName", "False"),
                new XElement("ProtectPassword", "True"),
                new XElement("ProtectURL", "False"),
                new XElement("ProtectNotes", "False")),
            new XElement("RecycleBinEnabled", Bool(meta.RecycleBinEnabled)),
            new XElement("RecycleBinUUID", meta.RecycleBinUuid.ToBase64()),
            new XElement("RecycleBinChanged", WriteTime(meta.RecycleBinChanged)),
            new XElement("EntryTemplatesGroup", meta.EntryTemplatesGroup.ToBase64()),
            new XElement("EntryTemplatesGroupChanged", WriteTime(meta.EntryTemplatesGroupChanged)),
            new XElement("HistoryMaxItems", meta.HistoryMaxItems.ToString(CultureInfo.InvariantCulture)),
            new XElement("HistoryMaxSize", meta.HistoryMaxSize.ToString(CultureInfo.InvariantCulture)),
            new XElement("SettingsChanged", WriteTime(meta.SettingsChanged)));

        if (meta.CustomData.Count > 0)
        {
            var cd = new XElement("CustomData");
            foreach (var (k, v) in meta.CustomData)
            {
                cd.Add(new XElement("Item", new XElement("Key", k), new XElement("Value", v)));
            }

            el.Add(cd);
        }

        return el;
    }

    private static XElement WriteGroup(Group group)
    {
        var el = new XElement(
            "Group",
            new XElement("UUID", group.Uuid.ToBase64()),
            new XElement("Name", group.Name),
            new XElement("Notes", group.Notes),
            new XElement("IconID", group.IconId.ToString(CultureInfo.InvariantCulture)),
            WriteTimes(group.Times),
            new XElement("IsExpanded", Bool(group.IsExpanded)),
            new XElement("DefaultAutoTypeSequence", group.DefaultAutoTypeSequence ?? string.Empty),
            new XElement("EnableAutoType", NullableBool(group.EnableAutoType)),
            new XElement("EnableSearching", NullableBool(group.EnableSearching)),
            new XElement("LastTopVisibleEntry", (group.LastTopVisibleEntry ?? KdbxId.Empty).ToBase64()));

        WriteCustomData(el, group.CustomData);

        foreach (var child in group.Groups)
        {
            el.Add(WriteGroup(child));
        }

        foreach (var entry in group.Entries)
        {
            el.Add(WriteEntry(entry, isHistory: false));
        }

        return el;
    }

    private static XElement WriteEntry(Entry entry, bool isHistory)
    {
        var el = new XElement(
            "Entry",
            new XElement("UUID", entry.Uuid.ToBase64()),
            new XElement("IconID", entry.IconId.ToString(CultureInfo.InvariantCulture)),
            new XElement("ForegroundColor", entry.ForegroundColor ?? string.Empty),
            new XElement("BackgroundColor", entry.BackgroundColor ?? string.Empty),
            new XElement("OverrideURL", entry.OverrideUrl ?? string.Empty),
            new XElement("QualityCheck", entry.QualityCheck),
            new XElement("Tags", string.Join(";", entry.Tags)),
            WriteTimes(entry.Times));

        foreach (var (key, value) in entry.Strings)
        {
            var valueEl = new XElement("Value", value.Value);
            if (value.IsProtected)
            {
                valueEl.SetAttributeValue("Protected", "True");
            }

            el.Add(new XElement("String", new XElement("Key", key), valueEl));
        }

        foreach (var (key, refIndex) in entry.Binaries)
        {
            el.Add(new XElement(
                "Binary",
                new XElement("Key", key),
                new XElement("Value", new XAttribute("Ref", refIndex.ToString(CultureInfo.InvariantCulture)))));
        }

        el.Add(WriteAutoType(entry.AutoType));
        WriteCustomData(el, entry.CustomData);

        if (!isHistory)
        {
            var history = new XElement("History");
            foreach (var h in entry.History)
            {
                history.Add(WriteEntry(h, isHistory: true));
            }

            el.Add(history);
        }

        return el;
    }

    private static XElement WriteAutoType(AutoTypeConfig config)
    {
        var el = new XElement(
            "AutoType",
            new XElement("Enabled", Bool(config.Enabled)),
            new XElement("DataTransferObfuscation", config.DataTransferObfuscation.ToString(CultureInfo.InvariantCulture)),
            new XElement("DefaultSequence", config.DefaultSequence ?? string.Empty));

        foreach (var assoc in config.Associations)
        {
            el.Add(new XElement(
                "Association",
                new XElement("Window", assoc.Window),
                new XElement("KeystrokeSequence", assoc.KeystrokeSequence)));
        }

        return el;
    }

    private static XElement WriteTimes(EntryTimes times) => new(
        "Times",
        new XElement("CreationTime", WriteTime(times.CreationTime)),
        new XElement("LastModificationTime", WriteTime(times.LastModificationTime)),
        new XElement("LastAccessTime", WriteTime(times.LastAccessTime)),
        new XElement("ExpiryTime", WriteTime(times.ExpiryTime)),
        new XElement("Expires", Bool(times.Expires)),
        new XElement("UsageCount", times.UsageCount.ToString(CultureInfo.InvariantCulture)),
        new XElement("LocationChanged", WriteTime(times.LocationChanged)));

    private static void WriteCustomData(XElement parent, Dictionary<string, string> data)
    {
        var el = new XElement("CustomData");
        foreach (var (k, v) in data)
        {
            el.Add(new XElement("Item", new XElement("Key", k), new XElement("Value", v)));
        }

        parent.Add(el);
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsProtected(XElement value) =>
        string.Equals((string?)value.Attribute("Protected"), "True", StringComparison.OrdinalIgnoreCase);

    private static string Bool(bool value) => value ? "True" : "False";

    private static string NullableBool(bool? value) => value switch
    {
        null => "null",
        true => "True",
        false => "False",
    };

    private static bool ReadBool(XElement? el, bool fallback) =>
        el is null ? fallback : string.Equals(el.Value, "True", StringComparison.OrdinalIgnoreCase);

    private static bool? ReadNullableBool(XElement? el) => el is null || string.Equals(el.Value, "null", StringComparison.OrdinalIgnoreCase)
        ? null
        : string.Equals(el.Value, "True", StringComparison.OrdinalIgnoreCase);

    private static int ReadInt(XElement? el, int fallback) =>
        el is not null && int.TryParse(el.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static long ReadLong(XElement? el, long fallback) =>
        el is not null && long.TryParse(el.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : fallback;

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private static KdbxId ReadId(XElement? el, bool generateIfMissing = false)
    {
        if (el is null || string.IsNullOrEmpty(el.Value))
        {
            return generateIfMissing ? KdbxId.New() : KdbxId.Empty;
        }

        try
        {
            return KdbxId.FromBase64(el.Value.Trim());
        }
        catch (FormatException)
        {
            return generateIfMissing ? KdbxId.New() : KdbxId.Empty;
        }
    }

    private static string WriteTime(DateTimeOffset? value)
    {
        long seconds = (long)((value ?? KdbxEpoch).ToUniversalTime() - KdbxEpoch).TotalSeconds;
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, seconds);
        return Convert.ToBase64String(buf);
    }

    private static DateTimeOffset? ReadTime(XElement? el)
    {
        if (el is null || string.IsNullOrEmpty(el.Value))
        {
            return null;
        }

        string raw = el.Value.Trim();

        // KDBX 4: base64 of Int64 LE seconds since 0001-01-01.
        if (TryFromBase64(raw, out byte[] bytes) && bytes.Length == 8)
        {
            long seconds = BinaryPrimitives.ReadInt64LittleEndian(bytes);
            return KdbxEpoch.AddSeconds(seconds);
        }

        // KDBX 3.1 / KeePassXC: ISO 8601.
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            return dto;
        }

        return null;
    }

    private static bool TryFromBase64(string s, out byte[] bytes)
    {
        bytes = [];
        Span<byte> buffer = stackalloc byte[16];
        if (Convert.TryFromBase64String(s, buffer, out int written))
        {
            bytes = buffer[..written].ToArray();
            return true;
        }

        return false;
    }
}
