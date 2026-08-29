using System.Text;

namespace AuraVault.Core.Import;

/// <summary>One parsed row: values keyed by header name, plus positional access.</summary>
public sealed class TabularRow
{
    private readonly IReadOnlyList<string> _headers;
    private readonly string[] _values;

    internal TabularRow(IReadOnlyList<string> headers, string[] values)
    {
        _headers = headers;
        _values = values;
    }

    public int FieldCount => _values.Length;

    public string this[int index] => index >= 0 && index < _values.Length ? _values[index] : string.Empty;

    public string this[string header]
    {
        get
        {
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i], header, StringComparison.OrdinalIgnoreCase))
                {
                    return this[i];
                }
            }

            return string.Empty;
        }
    }

    public bool HasColumn(string header) =>
        _headers.Any(h => string.Equals(h, header, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A header row plus data rows.</summary>
public sealed class TabularTable
{
    public required IReadOnlyList<string> Headers { get; init; }

    public required IReadOnlyList<TabularRow> Rows { get; init; }

    public char Delimiter { get; init; } = ',';
}

/// <summary>
/// A minimal RFC 4180 delimited-text reader with BOM handling and delimiter auto-detection
/// (<c>,</c> <c>;</c> tab <c>|</c>). Sufficient for password-manager exports; no external dependency.
/// </summary>
public static class DelimitedText
{
    private static readonly char[] Candidates = [',', ';', '\t', '|'];

    public static TabularTable Parse(string text, char? forceDelimiter = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.StartsWith('﻿'))
        {
            text = text[1..];
        }

        char delimiter = forceDelimiter ?? DetectDelimiter(text);
        var rows = ParseRows(text, delimiter);
        if (rows.Count == 0)
        {
            return new TabularTable { Headers = [], Rows = [], Delimiter = delimiter };
        }

        var headers = rows[0];
        var dataRows = new List<TabularRow>(rows.Count - 1);
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Length == 1 && rows[i][0].Length == 0)
            {
                continue; // blank trailing line
            }

            dataRows.Add(new TabularRow(headers, rows[i]));
        }

        return new TabularTable { Headers = headers, Rows = dataRows, Delimiter = delimiter };
    }

    public static TabularTable ParseFile(string path, char? forceDelimiter = null)
    {
        // FileShare.ReadWrite so an export still open in Excel doesn't block the import.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        string text = DecodeWithBomOrUtf8(ms.ToArray());
        return Parse(text, forceDelimiter);
    }

    private static string DecodeWithBomOrUtf8(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // Assume UTF-8 (covers plain ASCII too).
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(bytes);
    }

    private static char DetectDelimiter(string text)
    {
        // Use the first non-empty line; pick the candidate with the highest, most consistent count.
        int newline = text.IndexOfAny(['\r', '\n']);
        string firstLine = newline < 0 ? text : text[..newline];

        char best = ',';
        int bestCount = -1;
        foreach (char c in Candidates)
        {
            int count = firstLine.Count(ch => ch == c);
            if (count > bestCount)
            {
                bestCount = count;
                best = c;
            }
        }

        return bestCount <= 0 ? ',' : best;
    }

    private static List<string[]> ParseRows(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;

                case '\r':
                    // handled by the \n case; swallow a lone \r
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRow();
                    break;

                case '\n':
                    EndRow();
                    break;

                default:
                    if (c == delimiter)
                    {
                        EndField();
                    }
                    else
                    {
                        field.Append(c);
                    }

                    break;
            }
        }

        // trailing field / row
        if (field.Length > 0 || fields.Count > 0)
        {
            EndRow();
        }

        return rows;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            rows.Add([.. fields]);
            fields.Clear();
        }
    }
}
