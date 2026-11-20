namespace Daoris.Driver;

/// <summary>
/// A place's cast as its Settings and <c>daoris settings cast</c> say it: each role (<see cref="StepRoles.All"/>) with its holder now and why, the
/// place's risk and where it is set, the owner's marks covering it, the roster a cast may name, and the reporting line every role there reports
/// to: the place's supervisor, the machine's, the owner.
/// </summary>
public sealed record PlaceCastView(
    string Place, string Risk, string RiskSource, IReadOnlyList<string> Marks, IReadOnlyList<CastNow> Roles, IReadOnlyList<string> Roster, IReadOnlyList<LineLevel> Line, string? Unreadable = null)
{
    /// <summary>The view at the place <paramref name="workspace"/> and <paramref name="repository"/> name, neither the machine.</summary>
    /// <param name="agents">The agents this machine has (<see cref="ProviderRegistry.Agents"/>), each a holder <c>agent:&lt;name&gt;</c> a cast may name.</param>
    /// <param name="slots">The role catalogue's slots the reporting line is read from; none reads the line as the owner's alone.</param>
    public static PlaceCastView Of(
        PlaceCastBook book, PlaceRiskBook risk, string? workspace, string? repository, IReadOnlyList<string> agents, IReadOnlyList<RoleSlot>? slots, string? unreadable = null)
    {
        var standing = risk.At(workspace, repository);
        var roles = StepRoles.All.Select(role => book.At(role, workspace, repository, standing)).ToList();
        IReadOnlyList<string> roster = [CastHolders.Person, CastHolders.Agent, .. agents.Order(StringComparer.Ordinal).Select(CastHolders.AgentNamed)];
        IReadOnlyList<LineLevel> line = slots is null
            ? [ReportingLine.Owner]
            : ReportingLine.Of(slots, SupervisorKinds.Question, new RolePlace(workspace, repository), risk: risk) is var read ? [read.Place, read.Machine, ReportingLine.Owner] : [];
        return new(PlaceRiskBook.KeyOf(workspace, repository), standing.Risk, standing.Source, standing.Marks, roles, roster, line, unreadable);
    }

    /// <summary>The terminal's lines: the risk and marks, each role's holder and why, the roster and the line.</summary>
    public IEnumerable<string> Said()
    {
        yield return $"daoris: {Place}: {Risk} risk ({RiskSource}){(Marks.Count == 0 ? "" : $", marked {string.Join(", ", Marks.Select(mark => $"`{mark}`"))}")}.";
        foreach (var role in Roles)
        {
            var why = role.Why switch
            {
                CastWhy.Cast => $"cast ({role.Source}){(role.Words is { } words ? $": {words}" : "")}",
                CastWhy.Risk => $"the risk is high ({role.Source})",
                CastWhy.Default => "no cast names another",
                CastWhy.SignIn => "a sign-in is your act in a browser",
                _ => role.Why,
            };
            yield return $"  {role.Role,-20} {CastHolders.Said(role.Holder)} — {why}";
        }

        yield return $"  roster: {string.Join(", ", Roster)}";
        yield return $"  reports to: {string.Join(" → ", Line.Select(level => $"{LineLevels.Said(level.Level)}: {(level.Vacant ? "nobody" : level.Said())}"))}";
        if (Unreadable is { } problem) yield return $"  the casts do not read, so none is changed until they do (the last good read is shown): {problem}";
    }
}
