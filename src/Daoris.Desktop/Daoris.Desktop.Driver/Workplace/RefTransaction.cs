using System.Text.RegularExpressions;
using Daoris.Ports.Repositories;

namespace Daoris.Driver;

/// <summary>
/// One typed transaction for a set of ref acts, each with its expectation (create: absent; update and delete: the old value) and a verify
/// for each dependency only read. Two values or two names for one ref, and a name that is no full ref, are refused before git runs. Each
/// rollback is a conditional transaction too. The run is the repository port's (<see cref="IGitRepository.TransactAsync"/>).
/// </summary>
internal sealed class RefTransaction
{
    private enum Act { Create, Update, Delete }

    private sealed record Step(Act Act, string Ref, string? Old, string? New);

    private static readonly Regex Hash = new("^(?:[0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant);
    private static readonly Regex BadName = new(@"[\x00-\x20\x7f~^:?*\[\\]|\.\.|//|@\{|\.lock(/|$)|/\.|\.$|/$", RegexOptions.CultureInvariant);

    private readonly List<Step> _steps = [];
    private readonly List<(string Ref, string? Value)> _verifies = [];

    /// <summary>The ref made at <paramref name="value"/>, only where it is absent.</summary>
    public RefTransaction Create(string reference, string value) { _steps.Add(new(Act.Create, reference, null, value)); return this; }

    /// <summary>The ref set to <paramref name="value"/>, only where it stands at <paramref name="old"/>.</summary>
    public RefTransaction Update(string reference, string old, string value) { _steps.Add(new(Act.Update, reference, old, value)); return this; }

    /// <summary>The ref deleted, only where it stands at <paramref name="old"/>.</summary>
    public RefTransaction Delete(string reference, string old) { _steps.Add(new(Act.Delete, reference, old, null)); return this; }

    /// <summary>A dependency read, not moved: it must stand at <paramref name="value"/>, or be absent where that is null.</summary>
    public RefTransaction Verify(string reference, string? value) { _verifies.Add((reference, value)); return this; }

    public RefTransaction Verify(IEnumerable<(string Ref, string Commit)> held)
    {
        foreach (var (reference, commit) in held) Verify(reference, commit);
        return this;
    }

    /// <summary>A branch or ref moved from one name to another at <paramref name="tip"/>: the old name deleted, the new one made, both checked at once.</summary>
    public static RefTransaction Rename(string from, string to, string tip) => new RefTransaction().Delete(from, tip).Create(to, tip);

    public static RefTransaction ReverseRename(string from, string to, string tip) => new RefTransaction().Delete(to, tip).Create(from, tip);

    /// <summary>The rollback of a delete: the ref made again at <paramref name="value"/>, only where the name is absent.</summary>
    public static RefTransaction RestoreDeleted(string reference, string value) => new RefTransaction().Create(reference, value);

    /// <summary>The rollback of a rewrite: the ref set back to <paramref name="original"/>, only from the tip the rewrite <paramref name="produced"/>.</summary>
    public static RefTransaction RestoreRewritten(string reference, string produced, string original) =>
        new RefTransaction().Update(reference, produced, original);

    public IEnumerable<string> Names => _steps.Select(step => step.Ref).Concat(_verifies.Select(each => each.Ref)).Distinct(StringComparer.Ordinal);

    /// <summary>The input for <c>update-ref --stdin</c>: dependencies first, then each act; or why the transaction is refused, git not yet run.</summary>
    public (string? Input, string? Refusal) Build()
    {
        var (valid, refusal) = Validated();
        return valid is null ? (null, refusal) : (valid.Input(), null);
    }

    /// <summary>The transaction as the port runs it, each ref named once, with the dependencies an act already says left out; or why it is refused.</summary>
    public (GitRefTransaction? Valid, string? Refusal) Validated()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Names)
        {
            if (!name.StartsWith("refs/", StringComparison.Ordinal) || BadName.IsMatch(name)) return (null, $"`{name}` is not a full ref name");
            if (names.TryGetValue(name, out var other)) return (null, $"`{other}` and `{name}` are one ref under two names");
            names[name] = name;
        }

        var acts = new Dictionary<string, Step>(StringComparer.Ordinal);
        foreach (var step in _steps)
        {
            foreach (var value in new[] { step.Old, step.New })
            {
                if (value is not null && !Hash.IsMatch(value)) return (null, $"`{value}` is not a full object id for `{step.Ref}`");
            }

            if (!acts.TryAdd(step.Ref, step) && acts[step.Ref] != step) return (null, $"`{step.Ref}` is given two different acts");
        }

        var verifies = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (reference, value) in _verifies)
        {
            if (value is not null && !Hash.IsMatch(value)) return (null, $"`{value}` is not a full object id for `{reference}`");
            if (verifies.TryGetValue(reference, out var seen) && seen != value) return (null, $"`{reference}` is expected at two different values");
            verifies[reference] = value;
        }

        var checks = new List<GitRefCheck>();
        foreach (var (reference, value) in verifies)
        {
            // An act's own expectation already says it, and git refuses one ref named twice.
            if (acts.TryGetValue(reference, out var act))
            {
                if (act.Old != value) return (null, $"`{reference}` is expected at {Show(value)} and also at {Show(act.Old)}");
                continue;
            }

            checks.Add(new GitRefCheck(reference, value));
        }

        var made = acts.Values.Select(step => new GitRefAct(step.Act switch { Act.Create => GitRefActKind.Create, Act.Update => GitRefActKind.Update, _ => GitRefActKind.Delete }, step.Ref, step.Old, step.New)).ToList();
        return (new GitRefTransaction(made, checks), null);
    }

    private static string Show(string? value) => value ?? "absent";

    /// <summary>
    /// Runs the transaction: refused before the repository where it contradicts itself, else its own (<see cref="IGitRepository.TransactAsync"/>).
    /// Not <see cref="GitRefRun.Done"/> means nothing moved where git refused; where it did not answer (<see cref="GitRefRun.Unanswered"/>) the
    /// refs are read and said as they stand.
    /// </summary>
    public async Task<GitRefRun> RunAsync(string root, CancellationToken ct)
    {
        var (valid, refusal) = Validated();
        return valid is null ? new(false, refusal!) : await MachinePorts.Repository.TransactAsync(root, valid, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a rollback, and says in a sentence where a condition failed that nothing is moved: what stands at the names is then unknown
    /// to this step, and the caller reports it so.
    /// </summary>
    public async Task<GitRefRun> RollBackAsync(string root, string what, CancellationToken ct)
    {
        var run = await RunAsync(root, ct).ConfigureAwait(false);
        if (run.Done || run.Unanswered) return run.Unanswered ? run with { Said = $"{what} was not confirmed undone: {run.Said}" } : run;
        return new(false, $"{what} was not undone because a condition no longer held ({run.Said}); nothing was moved, so what stands is as the other side left it");
    }
}
