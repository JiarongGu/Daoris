using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// What a run's <c>tidy</c> ended of a landing's chain (D245 point 1), kept on the landing record as <c>tidied</c>: the landing's tip it ended
/// the chain at, when, and each part. The keeper and the press read it for a part a squashing chain carried, and judge that part no more.
/// </summary>
/// <param name="Tip">The landing's tip its pull request merged at, which the <c>tidy</c> node is reached on.</param>
public sealed record ChainTidied(string Tip, DateTimeOffset At)
{
    /// <summary>Each part's session branch the tidy ended, in the order the session record lists them.</summary>
    public IReadOnlyList<TidiedPart> Parts { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(ChainTidied? other) =>
        other is not null && Tip == other.Tip && At == other.At && Parts.SequenceEqual(other.Parts);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Tip, At, Parts.Count);
}

/// <summary>One part of a chain the tidy ended: its session branch at the commit it ended it at.</summary>
public sealed record TidiedPart(string Branch, string Tip)
{
    /// <summary>The recovery ref its commits are kept on, which the delete rested on; null where none was made.</summary>
    public string? KeptOn { get; init; }

    /// <summary>The journal's <c>ref.delete</c> act that removed its branch, which <c>GIT_REF_UNDO</c> undoes; null where none ran.</summary>
    public string? Act { get; init; }

    /// <summary>Why its branch stays, in a clause (checked out in a tree, moved since): the part is ended all the same; null where it went.</summary>
    public string? Kept { get; init; }
}

/// <summary>The tidy's record read and written: which part a landing's tidy ended, and the record's JSON.</summary>
internal static class ChainTidies
{
    /// <summary>
    /// The landing whose tidy ended <paramref name="branch"/> in <paramref name="repository"/> at <paramref name="tip"/>, and that part; null where
    /// none did. A branch that moved on since is work the tidy never saw, judged as any other.
    /// </summary>
    internal static (LandedBranch Landing, TidiedPart Part)? Of(IEnumerable<LandedBranch> landings, string repository, string branch, string tip)
    {
        foreach (var landing in landings.Reverse())
        {
            if (!string.Equals(landing.Repository, repository, StringComparison.OrdinalIgnoreCase) || landing.Tidied is not { } tidied) continue;
            if (tidied.Parts.FirstOrDefault(part => part.Branch == branch && string.Equals(part.Tip, tip, StringComparison.OrdinalIgnoreCase)) is { } ended)
            {
                return (landing, ended);
            }
        }

        return null;
    }

    /// <summary>The record as the landing entry's <c>tidied</c> property.</summary>
    internal static void Write(Utf8JsonWriter writer, ChainTidied tidied)
    {
        writer.WriteStartObject("tidied");
        writer.WriteString("tip", tidied.Tip);
        writer.WriteString("at", Moments.Write(tidied.At));
        writer.WriteStartArray("parts");
        foreach (var part in tidied.Parts)
        {
            writer.WriteStartObject();
            writer.WriteString("branch", part.Branch);
            writer.WriteString("tip", part.Tip);
            if (part.KeptOn is not null) writer.WriteString("keptOn", part.KeptOn);
            if (part.Act is not null) writer.WriteString("act", part.Act);
            if (part.Kept is not null) writer.WriteString("kept", part.Kept);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>The entry's <c>tidied</c>, or null where it holds none or one that does not read.</summary>
    internal static ChainTidied? Read(JsonElement entry)
    {
        if (!entry.TryGetProperty("tidied", out var tidied) || tidied.ValueKind != JsonValueKind.Object
            || Text(tidied, "tip") is not { } tip || Moments.Read(Text(tidied, "at")) is not { } at)
        {
            return null;
        }

        var parts = new List<TidiedPart>();
        if (tidied.TryGetProperty("parts", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var each in list.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object || Text(each, "branch") is not { } branch || Text(each, "tip") is not { } partTip) continue;
                parts.Add(new TidiedPart(branch, partTip) { KeptOn = Text(each, "keptOn"), Act = Text(each, "act"), Kept = Text(each, "kept") });
            }
        }

        return new ChainTidied(tip, at) { Parts = parts };
    }
}
