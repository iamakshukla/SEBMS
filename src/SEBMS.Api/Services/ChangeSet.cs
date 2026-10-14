using System.Globalization;

namespace SEBMS.Api.Services;

/// <summary>Collects "Field: before → after" lines for the fields that actually changed, for the audit log.</summary>
public sealed class ChangeSet
{
    private readonly List<string> _parts = [];

    public bool IsEmpty => _parts.Count == 0;

    public ChangeSet Add<T>(string field, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
            _parts.Add($"{field}: {Format(before)} → {Format(after)}");
        return this;
    }

    public override string ToString() => string.Join("; ", _parts);

    /// <summary>Plain, unambiguous text: invariant numbers and dates, quoted strings, "(none)" for null or empty.</summary>
    public static string Format(object? value) => value switch
    {
        null => "(none)",
        string { Length: 0 } => "(none)",
        string s => $"\"{s}\"",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "(none)"
    };
}
