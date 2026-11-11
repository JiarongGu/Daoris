namespace Daoris.Driver;

/// <summary>A <c>content.holds</c>' reading: reached once nothing the line holds of the work holds it, else none.</summary>
internal static class ContentHoldsReading
{
    /// <summary>Whether work the line or a branch of the person's holds by content holds it.</summary>
    public const string Line = "line";

    /// <summary>Whether the unlanded-commit proof holds it, where no commit is left that a branch of the person's lacks.</summary>
    public const string Proof = "proof";

    /// <summary>Its options, in the order an editor lists them; each is <c>true</c> or <c>false</c>, and true where it is not written.</summary>
    public static readonly IReadOnlyList<string> Options = [Line, Proof];

    /// <summary>Why what the line holds holds the work, or null where it lets it go.</summary>
    public static string? Held(HoldContent read, bool line = true, bool proof = true) =>
        line && read.Carried is { } carried ? carried.TrimEnd()
        : !proof ? null
        : read.Unlanded is null ? "git could not say which of its commits no branch of yours holds"
        : read.Unlanded > 0 ? null
        : read.Ahead == 0 ? "it holds no commit the line lacks: nothing to land"
        : "a branch of yours already holds each of its commits (D88's proof), so nothing is landed again";

    public static NodeObservation Observe(NodeReading reading)
    {
        if (HoldReading.Settled(reading) is { } settled) return settled;
        var (unit, node, binding) = (reading.Unit, reading.Node, reading.Binding);
        if (unit.Kind != WorkUnitKind.Branch) return HoldReading.Seen(reading, null, $"it reads a branch's work, and this is a {unit.Kind}");
        if (reading.Git?.Content?.Invoke(unit.Id, binding.SubjectRevision) is not { } read)
        {
            return HoldReading.Seen(reading, null, "what the line holds of it was not read at the bound commit");
        }

        if (read.Line is { HoldsAll: true } onLine)
        {
            return HoldReading.Reached(reading, "git", binding.SubjectRevision, onLine.Said);
        }

        return Held(read, HoldReading.On(node, Line), HoldReading.On(node, Proof)) is { } held
            ? HoldReading.Seen(reading, null, held)
            : HoldReading.Reached(reading, "git", binding.SubjectRevision, $"{read.Unlanded} of its commits no branch of yours holds, and the line does not hold it by content");
    }
}
