namespace Daoris.Driver;

/// <summary>
/// The units asked for while a pass works (<see cref="NodeRuntimeLook.Late"/>), taken into that pass's own turn at each unit boundary, ahead of
/// what remains, and judged by it once each: no second pass runs, so the pass's tips, bounds and lock are the only ones. A unit judged here is
/// left out of the turn's later entries; a unit the turn judged already is judged again where it is asked again, since the fact may have
/// landed after the turn read its record.
/// </summary>
internal sealed class LateUnits
{
    private readonly NodeRuntimeLook _look;
    private readonly IReadOnlyDictionary<string, GovernedUnit> _governed;
    private readonly HashSet<string> _judged = new(StringComparer.Ordinal);

    /// <param name="look">The running pass's look; each unit it asks for that is not governed is told so at once.</param>
    /// <param name="governed">The units the pass judges under, by id.</param>
    public LateUnits(NodeRuntimeLook look, IReadOnlyDictionary<string, GovernedUnit> governed)
    {
        (_look, _governed) = (look, governed);
        foreach (var unit in look.Asked.Where(unit => !governed.ContainsKey(unit))) look.Ungoverned(unit);
    }

    /// <summary>Whether this took <paramref name="unit"/> as a late ask: the turn leaves its own entry for it out.</summary>
    public bool Judged(string unit) => _judged.Contains(unit);

    /// <summary>
    /// Judges each unit asked for since the last boundary, once, in the pass's own turn: its checkout read afresh in <paramref name="tips"/>, the
    /// words kept on its record applied, its attempts reconciled (a check's or a plugin's answer is read only there), then
    /// <paramref name="evaluate"/> within the pass's bounds. Where not every attempt could be asked, its door answers not judged and the turn
    /// keeps it; a failure fails that door alone, and the unit is not marked judged.
    /// </summary>
    public async Task JudgeAsync(
        NodeRunKeeping keeping, PassTips tips, List<WorkPerformed> done, List<string> said,
        Func<GovernedUnit, Task<(IReadOnlyList<WorkPerformed> Done, bool Whole)>> reconcile, Func<GovernedUnit, Task> evaluate, CancellationToken ct)
    {
        foreach (var each in Take())
        {
            try
            {
                using var asking = GitAskers.Is(GitAskers.Steps);
                tips.Refresh(each.Watched.Root);
                if (each.Scope is { } scope) tips.Refresh(scope.Root);
                var worded = await keeping.WordedAsync([each], ct, (unit, at) => tips.TipAsync(at.Root, unit.Watched.Branch, ct)).ConfigureAwait(false);
                done.AddRange(worded.Done);
                said.AddRange(worded.Said);
                var (reconciled, whole) = await reconcile(each).ConfigureAwait(false);
                done.AddRange(reconciled);
                if (!whole)
                {
                    _look.NotJudged(each.Unit.Id);
                    said.Add($"work  {each.Unit.Id} has an attempt not asked after at its ask (the pass's calls are spent or its process did not answer), and is judged at the turn");
                    continue;
                }

                await evaluate(each).ConfigureAwait(false);
                _judged.Add(each.Unit.Id);
                _look.Judged(each.Unit.Id);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
            {
                _look.Failed(each.Unit.Id);
                said.Add($"work  {each.Unit.Id} was not judged at its ask, and is judged again at the next look: {error.Message}");
            }
        }
    }

    /// <summary>The governed units asked for since the last take, to be judged now; the others are told they are not governed.</summary>
    private IReadOnlyList<GovernedUnit> Take()
    {
        var taken = new List<GovernedUnit>();
        foreach (var unit in _look.Late())
        {
            if (!_governed.TryGetValue(unit, out var each))
            {
                _look.Ungoverned(unit);
                continue;
            }

            taken.Add(each);        }

        return taken;
    }
}
