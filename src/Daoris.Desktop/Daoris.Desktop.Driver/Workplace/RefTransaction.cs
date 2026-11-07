using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a ref transaction did: <see cref="Done"/>; or, git having refused with a real exit, why nothing moved; or, <see cref="Unanswered"/>
/// (git never started, or ended without an exit), the refs as they stand after a read of each, said in a sentence.
/// </summary>
internal readonly record struct RefRun(bool Done, string Said, bool Unanswered = false);

/// <summary>
/// One typed transaction for a set of ref acts: exactly one <c>git update-ref --stdin</c> operation per ref, each with its
/// expectation (create: the ref absent; update and delete: its old value), and a <c>verify</c> for each dependency that is only read.
/// Identical expectations are named once; two values for one ref, two names for one ref (a case alias, a symbolic ref) and a name that is
/// no full ref are refused before git runs, so git's own "multiple updates" never decides what a caller meant.
/// </summary>
/// <remarks>
/// Git checks every line under its locks and moves nothing unless all hold, so a refusal means nothing moved. The rollbacks are
/// transactions too, each conditional: a deleted ref is restored only where the name is absent, a rewritten one only from the tip the
/// rewrite produced, a rename reversed checking both names at once.
/// </remarks>
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

    /// <summary>The same dependencies as <c>(Ref, Commit)</c> pairs, as a proof returns them.</summary>
    public RefTransaction Verify(IEnumerable<(string Ref, string Commit)> held)
    {
        foreach (var (reference, commit) in held) Verify(reference, commit);
        return this;
    }

    /// <summary>A branch or ref moved from one name to another at <paramref name="tip"/>: the old name deleted, the new one made, both checked at once.</summary>
    public static RefTransaction Rename(string from, string to, string tip) => new RefTransaction().Delete(from, tip).Create(to, tip);

    /// <summary>The rollback of <see cref="Rename"/>: the new name deleted only at <paramref name="tip"/>, the old made only where it is absent.</summary>
    public static RefTransaction ReverseRename(string from, string to, string tip) => new RefTransaction().Delete(to, tip).Create(from, tip);

    /// <summary>The rollback of a delete: the ref made again at <paramref name="value"/>, only where the name is absent.</summary>
    public static RefTransaction RestoreDeleted(string reference, string value) => new RefTransaction().Create(reference, value);

    /// <summary>The rollback of a rewrite: the ref set back to <paramref name="original"/>, only from the tip the rewrite <paramref name="produced"/>.</summary>
    public static RefTransaction RestoreRewritten(string reference, string produced, string original) =>
        new RefTransaction().Update(reference, produced, original);

    /// <summary>Every ref the transaction names, as given.</summary>
    public IEnumerable<string> Names => _steps.Select(step => step.Ref).Concat(_verifies.Select(each => each.Ref)).Distinct(StringComparer.Ordinal);

    /// <summary>The input for <c>update-ref --stdin</c>: dependencies first, then each act; or why the transaction is refused, git not yet run.</summary>
    public (string? Input, string? Refusal) Build()
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

        var text = new StringBuilder();
        foreach (var (reference, value) in verifies)
        {
            // An act's own expectation already says it, and git refuses one ref named twice.
            if (acts.TryGetValue(reference, out var act))
            {
                if (act.Old != value) return (null, $"`{reference}` is expected at {Show(value)} and also at {Show(act.Old)}");
                continue;
            }

            text.Append($"verify {reference} {value}".TrimEnd()).Append('\n');
        }

        foreach (var step in acts.Values)
        {
            text.Append(step.Act switch
            {
                Act.Create => $"create {step.Ref} {step.New}",
                Act.Update => $"update {step.Ref} {step.New} {step.Old}",
                _ => $"delete {step.Ref} {step.Old}",
            }).Append('\n');
        }

        return (text.ToString(), null);
    }

    private static string Show(string? value) => value ?? "absent";

    /// <summary>
    /// Runs the transaction: refused before git where it contradicts itself, refused where a name is a symbolic ref (git would follow it to
    /// another name, or, on a case-blind disk, a create beside its case-alias), else one <c>update-ref --stdin</c>. Not <see cref="RefRun.Done"/>
    /// means nothing moved where git refused; where it did not answer (<see cref="RefRun.Unanswered"/>) the refs are read and said as they stand.
    /// </summary>
    public async Task<RefRun> RunAsync(string root, CancellationToken ct)
    {
        var (input, refusal) = Build();
        if (refusal is not null) return new(false, refusal);
        var names = Names.ToList();
        if (names.Count > 0)
        {
            var (listed, rows, listErr) = await WorkingTree.GitAsync(
                root, ["for-each-ref", "--format=%(refname)%09%(symref)", "--", .. names], ct).ConfigureAwait(false);
            if (listed != 0) return new(false, $"whether a name is a symbolic ref is unread ({ContentHolds.Said(listErr)})");
            foreach (var row in rows.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = row.TrimEnd('\r').Split('\t');
                if (parts.Length == 2 && parts[1].Length > 0 && names.Contains(parts[0])) return new(false, $"`{parts[0]}` is a symbolic ref to `{parts[1]}`");
            }
        }

        if (await AliasStandingAsync(root, ct).ConfigureAwait(false) is { } alias) return new(false, alias);

        // One act and nothing to verify is one command line, the same operation without a stdin.
        var (code, _, err) = _verifies.Count == 0 && _steps.Count == 1 && Single() is { } line
            ? await WorkingTree.GitAsync(root, line, ct).ConfigureAwait(false)
            : await WorkingTree.GitAsync(root, ["update-ref", "--stdin"], input!, ct).ConfigureAwait(false);
        if (code == 0) return new(true, "");
        // -1 is no answer: the bound reached or git never started. Only the refs themselves say whether anything moved.
        return code == -1 ? await UnansweredAsync(root, err, ct).ConfigureAwait(false) : new(false, ContentHolds.Said(err));
    }

    /// <summary>
    /// Where the disk is case-blind, a create of a name whose case-alias already stands (loose or packed) is refused before git runs:
    /// the file system would make the two one file. Null where no create is aliased.
    /// </summary>
    private async Task<string?> AliasStandingAsync(string root, CancellationToken ct)
    {
        var creates = _steps.Where(step => step.Act == Act.Create).ToList();
        if (creates.Count == 0) return null;
        var (blind, said, configErr) = await WorkingTree.GitAsync(root, ["config", "--type=bool", "--get", "core.ignorecase"], ct).ConfigureAwait(false);
        if (blind == -1) return $"whether the disk tells names apart by case is unread ({ContentHolds.Said(configErr)})";
        if (blind != 0 || said.Trim() != "true") return null;

        foreach (var prefix in creates.Select(step => step.Ref[..(step.Ref.LastIndexOf('/') + 1)]).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (code, rows, err) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", "--", prefix], ct).ConfigureAwait(false);
            if (code != 0) return $"which refs stand under `{prefix}` is unread ({ContentHolds.Said(err)})";
            var standing = rows.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r')).ToList();
            foreach (var step in creates)
            {
                var other = standing.FirstOrDefault(each =>
                    string.Equals(each, step.Ref, StringComparison.OrdinalIgnoreCase) && !string.Equals(each, step.Ref, StringComparison.Ordinal));
                if (other is not null) return $"`{step.Ref}` would be one ref with `{other}`, which stands, on a disk that reads names without case";
            }
        }

        return null;
    }

    /// <summary>Reads each ref the transaction moves and says how it stands: moved (at the new value), not moved (at the old), or unknown.</summary>
    private async Task<RefRun> UnansweredAsync(string root, string err, CancellationToken ct)
    {
        var told = new List<string>();
        foreach (var step in _steps)
        {
            var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", step.Ref], ct).ConfigureAwait(false);
            var stands = code == 0 ? at.Trim() : code == 1 ? null : "?";
            told.Add(stands == "?" ? $"`{step.Ref}` unknown (unread)"
                : stands == step.New ? $"`{step.Ref}` moved"
                : stands == step.Old ? $"`{step.Ref}` not moved"
                : $"`{step.Ref}` unknown (stands at {Show(stands is { Length: > 12 } ? stands[..12] : stands)}, neither as it was nor as it was to be)");
        }

        return new(false, $"git did not answer ({ContentHolds.Said(err)}); as the refs stand: {string.Join(", ", told)}", Unanswered: true);
    }

    private string[]? Single() => _steps[0] switch
    {
        { Act: Act.Create } step => ["update-ref", step.Ref, step.New!, ""],
        { Act: Act.Update } step => ["update-ref", step.Ref, step.New!, step.Old!],
        _ => null,
    };

    /// <summary>
    /// Runs a rollback, and says in a sentence where a condition failed that nothing was moved: what stands at the names is then unknown
    /// to this step, and the caller reports it so.
    /// </summary>
    public async Task<RefRun> RollBackAsync(string root, string what, CancellationToken ct)
    {
        var run = await RunAsync(root, ct).ConfigureAwait(false);
        if (run.Done || run.Unanswered) return run.Unanswered ? run with { Said = $"{what} was not confirmed undone: {run.Said}" } : run;
        return new(false, $"{what} was not undone because a condition no longer held ({run.Said}); nothing was moved, so what stands is as the other side left it");
    }
}
