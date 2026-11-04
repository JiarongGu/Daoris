namespace Daoris.Knowledge;

/// <summary>What a decision entry's own file says of it: the notes after it and the marks that target it.</summary>
internal static class DecisionMarks
{
    /// <summary>A decision entry's notes after it and the marks that target it, from the entries of its file; null where they cannot be placed.</summary>
    public static DecisionStanding? StandingOf(KnowledgeEntry entry, IReadOnlyList<KnowledgeEntry> file) =>
        On(entry, file) is not { } placed
            ? null
            : new DecisionStanding(
                placed.Later,
                [.. placed.Targeted.Select(mark => $"{mark.Kind} by {(mark.By is { } by ? $"D{by}" : "a later note here")}")]);

    /// <summary>How many notes of its file follow a decision entry, and the marks that target it in line order; null where they cannot be placed.</summary>
    public static (int Later, IReadOnlyList<NoteMark> Targeted)? On(KnowledgeEntry entry, IReadOnlyList<KnowledgeEntry> file)
    {
        var decision = file.Where(held => held.Kind == EntryKind.Decision).ToList();
        if (decision.All(held => held.Anchor is not null)) return null;

        var notes = decision.Where(held => held.Anchor is not null && held.Lines is not null).ToList();
        var marks = DecisionNotes.Marks(notes);
        IEnumerable<NoteMark> targeted;
        int later;
        if (entry.Anchor is null)
        {
            targeted = marks.Where(mark => mark.Target is null && mark.By is not null);
            later = notes.Count;
        }
        else if (entry.Lines is { } own)
        {
            targeted = marks.Where(mark => mark.Target == own.First);
            later = notes.Count(note => note.Lines!.Value.First > own.First);
        }
        else
        {
            return null;
        }

        return (later, [.. targeted.OrderBy(mark => mark.Line)]);
    }

    /// <summary>The first mark of this kind its own record puts on a decision entry, or null.</summary>
    public static NoteMark? Marked(KnowledgeEntry entry, HeldTogether held, string kind) =>
        held.Files.TryGetValue((entry.Repository, entry.RelativePath), out var file) && On(entry, file) is { } placed
            ? placed.Targeted.FirstOrDefault(mark => mark.Kind == kind)
            : null;
}
