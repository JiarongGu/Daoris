namespace Daoris.Driver;

/// <summary>The account of what a session working in its place is told of it (<see cref="PlacePrompt"/>), from its start's manifest.</summary>
internal static class PlaceAccount
{
    public static HandedSection Of(PlaceTold? told, int chars, string size)
    {
        var (outside, others) = (told?.Outside.Count ?? 0, told?.Others.Count ?? 0);
        var left = PlacePrompt.Left(told);
        return new HandedSection(
            HandedSections.Place, HandedSources.Tree,
            $"what changed in its place: {size}, {InstructionAccounting.Plural(outside, "path")} changed outside Daoris since the last "
            + $"session there and {InstructionAccounting.Plural(others, "uncommitted path")} not its own, read from its start's manifest")
        {
            Chars = chars,
            Shown = outside + others - left,
            Of = outside + others,
            Cuts = left == 0
                ? null
                : [new HandedCut(HandedCuts.BriefList, $"{left} entries of its lists left out past their bound of {PlacePrompt.Limit}") { Count = left }],
        };
    }
}
