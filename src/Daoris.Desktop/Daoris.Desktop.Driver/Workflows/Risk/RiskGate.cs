namespace Daoris.Driver;

/// <summary>Where an act happens, as the judge reads it: its workspace and repository (none: a wider place), and the place's access mode.</summary>
/// <param name="Full">Whether the place's harness reaches anything: its permission prompts resolve unrestricted (<see cref="AccessRules.Resolve"/>, as full access gives, or those alone): the mode rows keyed by one apply under, and the seeds of the acts that read reach.</param>
public sealed record RiskPlace(string? Workspace, string? Repository, bool Full)
{
    public const string FullMode = "full";
    public const string RestrictedMode = "restricted";

    public string Mode => Full ? FullMode : RestrictedMode;

    /// <summary>The place <paramref name="workspace"/> and <paramref name="repository"/> name, its access mode read from <paramref name="config"/>.</summary>
    public static RiskPlace Of(DriverConfig config, string? workspace, string? repository)
    {
        var named = repository?.Trim() is { Length: > 0 } r ? r : null;
        var access = named is not null
            ? AccessRules.Resolve(config, named, workspace)
            : AccessRules.ResolveLevels(null, workspace is { Length: > 0 } w && config.WorkspaceAccess.TryGetValue(w, out var shared) ? shared : null, config.MachineAccess);
        return new(workspace is { Length: > 0 } ? workspace : null, named, access.Value(AccessRules.Permissions) == AccessRules.Unrestricted);
    }

    /// <summary>The policy's place keys from this place up to the machine, narrowest first.</summary>
    public IReadOnlyList<string> Chain() =>
    [
        .. Repository is { } repository ? [$"repository/{repository}"] : Array.Empty<string>(),
        .. Workspace is { } workspace ? [$"workspace/{workspace}"] : Array.Empty<string>(),
        PlaceRiskBook.MachineKey,
    ];
}

/// <summary>
/// The one judge of every hold: model-free and pure, act, actor, place and subject against the policy give a verdict with the
/// rule that decided. A <c>release.production</c> mark anywhere on the place's chain keeps every <c>release.*</c> act at the owner, over every
/// row. Otherwise the narrowest place with a row for the act decides (repository, workspace, machine), a qualified row standing over its
/// kind's and a row keyed by the place's mode over one keyed by none; then a level carried from <c>placeRisk</c> (a place at <c>high</c>, every
/// kind at the owner); then the seed. In shadow it decides nothing: <see cref="RiskShadow"/> journals what it would say beside today's gates.
/// </summary>
public static class RiskGate
{
    /// <summary>The verdict on <paramref name="act"/> by <paramref name="actor"/> at <paramref name="place"/> on <paramref name="subject"/>.</summary>
    /// <remarks>The actor and the subject are recorded with the verdict and judge nothing: a worker answering its own gate is refused at the settle door (<see cref="SettleDoor.Own"/>).</remarks>
    public static RiskVerdict Judge(RiskPolicy policy, RiskAct act, string actor, RiskPlace place, string subject)
    {
        _ = (actor, subject);
        RiskVerdict Verdict(string riskClass, string? by, string source, string reason)
        {
            var known = RiskClasses.Is(riskClass);
            var read = known ? riskClass : RiskClasses.Owner;
            var said = known ? reason : $"{reason}; the class `{riskClass}` is none this build knows, so it reads `owner`";
            return new(act.Kind, act.Qualifier, RiskVerdict.OutcomeOf(read), read, read == RiskClasses.Role ? by ?? DefaultRole(act.Kind) : null, source, policy.Revision, said);
        }

        if (act.Kind == RiskActs.PolicyChange)
        {
            return Verdict(RiskClasses.Owner, null, RiskVerdict.Fixed, "a change to who holds power is the owner's own, fixed: no row changes it");
        }

        if (policy.Fault is { } fault)
        {
            return Verdict(RiskClasses.Owner, null, RiskVerdict.Unreadable, $"the risk policy does not read ({fault}), so every act reads at the owner until it is mended");
        }

        var chain = place.Chain();
        if (act.Kind.StartsWith("release.", StringComparison.Ordinal)
            && chain.FirstOrDefault(key => policy.PlaceRisk.Places.GetValueOrDefault(key)?.Marked(PlaceMarks.ProductionReleases) == true) is { } marked)
        {
            return Verdict(RiskClasses.Owner, null, marked, $"`{marked}` is marked `{PlaceMarks.ProductionReleases}` in `placeRisk`, which keeps every release act at the owner");
        }

        var leveled = chain.FirstOrDefault(key => policy.PlaceRisk.Places.ContainsKey(key));
        foreach (var key in chain)
        {
            if (Row(policy, key, act, place.Mode) is { } row)
            {
                return Verdict(row.Class, row.By, key, $"`{key}` classes {Spelled(row)} `{row.Class}`");
            }

            var level = policy.PlaceRisk.Places.GetValueOrDefault(key);
            if (key == leveled && level?.Risk == PlaceRisks.High)
            {
                return Verdict(RiskClasses.Owner, null, key, $"`{key}` is at high risk in `placeRisk`, read as every act at the owner there");
            }
        }

        var seed = RiskActs.Seed(act.Kind, place.Full);
        return Verdict(seed, null, RiskVerdict.Seed, SeedReason(act.Kind, seed, place));
    }

    /// <summary>Each kind of the census judged unqualified at <paramref name="place"/>, in the census's order: the resolved table.</summary>
    public static IReadOnlyList<RiskVerdict> Table(RiskPolicy policy, RiskPlace place) =>
        [.. RiskActs.All.Select(kind => Judge(policy, new RiskAct(kind, null), "", place, ""))];

    /// <summary>The role a role-classed act goes to where its row names none: a held permission the place's approver, any other its supervisor.</summary>
    public static string DefaultRole(string kind) => kind == RiskActs.ReachWiden ? StepRoles.Approver : AppointedRoles.Supervisor;

    private static RiskRow? Row(RiskPolicy policy, string key, RiskAct act, string mode) =>
        policy.Rows.GetValueOrDefault(key)?
            .Where(row => row.Kind == act.Kind && (row.Qualifier is null || row.Qualifier == act.Qualifier) && (row.Mode is null || row.Mode == mode))
            .OrderByDescending(row => row.Qualifier is not null)
            .ThenByDescending(row => row.Mode is not null)
            .FirstOrDefault();

    private static string Spelled(RiskRow row) =>
        (row.Qualifier is { } qualifier ? $"`{row.Kind}/{qualifier}`" : $"`{row.Kind}`") + (row.Mode is { } mode ? $" under {mode} access" : "");

    private static string SeedReason(string kind, string seed, RiskPlace place) => kind switch
    {
        RiskActs.CheckNarrow => "a check's reach narrowed passes, said in one line (the seed)",
        RiskActs.ReachWiden or RiskActs.CommandChange => $"under {place.Mode} access the seed classes it `{seed}`",
        RiskActs.HistoryRewrite or RiskActs.DataDelete => "the owner's by the seed, which the owner may change",
        _ => "no risk declared: it passes, recorded (the seed)",
    };
}
