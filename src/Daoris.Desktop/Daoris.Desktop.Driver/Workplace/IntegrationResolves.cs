namespace Daoris.Driver;

/// <summary>
/// What a member's resolve made of its merge (INTEGRATE1e part e3b; the integration design §5.2 step 4): the files it settled by the
/// repository's own rules, said, and the ones it could not, which are conflicts of meaning (step 5).
/// </summary>
internal sealed record IntegrationResolved
{
    /// <summary>The conflicted files no rule settles, or whose rule could not finish; none where the merge goes on to its commit.</summary>
    public IReadOnlyList<string> Unsettled { get; init; } = [];

    /// <summary>Why the files in <see cref="Unsettled"/> stay conflicts where that is not simply that no rule settles them (a line rule's writer failed).</summary>
    public string? Refused { get; init; }

    /// <summary>The backlog the merge base settled, summed over its conflict; null where none was.</summary>
    public BacklogMerge.Settled? Backlog { get; init; }

    /// <summary>The declared row tables the merge base settled.</summary>
    public IReadOnlyList<BacklogMerge.Settled> Tables { get; init; } = [];

    /// <summary>The generated files that took the candidate's copy.</summary>
    public IReadOnlyList<string> Taken { get; init; } = [];

    /// <summary>What the accepted writers wrote again over <see cref="Taken"/>, inside the merge's commit; nothing run where none is accepted.</summary>
    public GeneratedWriters.Written Written { get; init; } = GeneratedWriters.Written.None;

    /// <summary>The files an accepted line rule settled, with their writers.</summary>
    public IReadOnlyList<LineRules.Settled> Lined { get; init; } = [];

    /// <summary>The joins union left that were set apart in the merge.</summary>
    public IReadOnlyList<UnionJoins.Set> Joined { get; init; } = [];

    /// <summary>The merge goes on to its commit.</summary>
    public bool Stands => Unsettled.Count == 0;

    /// <summary>What the resolve settled, in sentences after the replay's own; nothing where it settled nothing.</summary>
    public string Said =>
        (Backlog is { } backlog ? Table(backlog.Path, "The backlog", backlog.Kept, backlog.Dropped) : "")
        + string.Concat(Tables.Select(table => Table(table.Path, "The declared row table", table.Kept, table.Dropped)))
        + (Joined.Count == 0 ? "" : " Where union had joined it to the line above, it was set apart: " + string.Join(", ", Joined.Select(each => $"`{each.Path}:{each.Line}`")) + ".")
        + (Taken.Count == 0 ? "" : $" {string.Join(", ", Taken.Select(path => $"`{path}`"))} took the candidate's copy, a generated file" + Writing())
        + (Lined.Count == 0 ? ""
            : $" The declared line rule settled {string.Join(", ", Lined.Select(each => $"`{each.Path}`"))}, written again from the merged tree by "
              + string.Join(", ", Lined.Select(each => $"`{each.Run}`").Distinct(StringComparer.Ordinal)) + ".");

    private static string Table(string path, string what, int kept, int dropped) =>
        $" {what} `{path}` conflicted and was settled by the merge base: {kept} line(s) a side added kept, {dropped} line(s) a side removed dropped.";

    private string Writing() =>
        Written.Failed is { } failed ? $", which was not written again ({failed}), so the candidate's copy stays."
        : Written.Runs.Count > 0 ? $", then written again from the merged tree by {string.Join(", ", Written.Runs.Select(run => $"`{run}`"))}."
        : ", and no writer is accepted for it to be written again.";
}

/// <summary>
/// A member's resolve (INTEGRATE1e part e3b; the integration design §5.2 step 4), run inside the member's replay effect between its
/// <c>merge --no-commit</c> and its commit: each conflicted path judged by the rules the repository declares, never by any that its own work
/// brings, and the merge left for the commit where every one is settled. The pieces are the landing's replay's (<see cref="BranchReplays"/>),
/// called, not copied: <see cref="BacklogMerge.Stops"/> for the backlog, the declared row tables and the accepted line rules,
/// <see cref="GeneratedPaths"/> and <see cref="GeneratedWriters"/> for the generated files, <see cref="UnionJoins"/> for the joins union left.
/// </summary>
/// <remarks>
/// <para><b>Declared at the line.</b> Which files are generated, the backlog and its row tables, the line rules and the writers are read as the
/// candidate's base holds them (the line's commit it was formed on), so an earlier member's declaration changes nothing for a later one, as
/// the merge tool reads the row tables of <c>base</c> (<c>mergeMember</c>), and a session's own declaration is never read. The writers and the
/// line rules run only as the person accepted them for integration (<see cref="IntegrationAcceptance"/>); with no config handed in nothing is run.</para>
/// <para><b>One commit.</b> Everything staged here is part of the member's merge commit, whose second parent is the frozen tip: the set-apart
/// joins and the written files are not commits on top, as the merge tool's candidate merge makes none either. A conflict it cannot settle is
/// returned for the caller to abort; nothing here aborts, so the caller's reset is the one place the tree is put back.</para>
/// </remarks>
internal static class IntegrationResolves
{
    /// <summary>
    /// <paramref name="unmerged"/> (the conflicted paths git left, none for a merge that went clean) settled where a rule settles them, the
    /// joins union left set apart, the generated files and line-rule files written again; staged, never committed.
    /// </summary>
    /// <param name="lineBase">The line's commit the candidate was formed on: where the declarations are read.</param>
    /// <param name="onto">The candidate's tip so far, which the merge was made onto.</param>
    /// <param name="tip">The member's frozen tip, the merge's second parent.</param>
    /// <param name="config">The person's choices the acceptances are read from, as the replay reads them; null runs no writer and settles no line rule.</param>
    internal static async Task<IntegrationResolved> ResolveAsync(
        string tree, string root, string repository, string workspace, string lineBase, string onto, string tip, IReadOnlyList<string> unmerged,
        DriverConfig? config, CancellationToken ct)
    {
        var taken = new List<string>();
        BacklogMerge.Stops? stops = null;
        if (unmerged.Count > 0)
        {
            var lines = config is null ? [] : await LineRules.AcceptedAsync(config, root, repository, lineBase, ct).ConfigureAwait(false);
            stops = new BacklogMerge.Stops(lineBase, lines);
            var declared = await stops.DeclaredAsync(tree, ct).ConfigureAwait(false);
            // git unable to say which are generated reads as none: such a path is a conflict.
            var marked = await GeneratedPaths.MarkedAsync(tree, [.. unmerged.Where(path => !declared.Settles(path))], ct, lineBase).ConfigureAwait(false)
                         ?? new HashSet<string>(StringComparer.Ordinal);
            var unsettled = new List<string>();
            foreach (var path in unmerged)
            {
                if (declared.Settles(path))
                {
                    if (!await stops.SettleAsync(tree, path, ct).ConfigureAwait(false)) unsettled.Add(path);
                }
                else if (marked.Contains(path))
                {
                    // The candidate's side, `--ours` in a merge made onto it; a path it removed goes, working file with it.
                    var (took, _, _) = await WorkingTree.GitAsync(tree, ["checkout", "--ours", "--", path], ct).ConfigureAwait(false);
                    await WorkingTree.GitAsync(tree, took == 0 ? ["add", "--", path] : ["rm", "--quiet", "--force", "--", path], ct).ConfigureAwait(false);
                    taken.Add(path);
                }
                else
                {
                    unsettled.Add(path);
                }
            }

            if (unsettled.Count > 0) return new IntegrationResolved { Unsettled = unsettled };
        }

        // What union joined is set apart before anything is written again, as the merge tool sets joins apart before its commit.
        var joined = await UnionJoins.StageSetApartAsync(tree, onto, tip, lineBase, merging: true, ct).ConfigureAwait(false);

        var written = GeneratedWriters.Written.None;
        if (taken.Count > 0 && config is not null)
        {
            var writers = await GeneratedWriters.AcceptedAsync(config, root, repository, workspace, lineBase, ct).ConfigureAwait(false);
            written = await GeneratedWriters.WriteAsync(tree, writers, taken, ct, commit: false).ConfigureAwait(false);
        }

        var lined = stops?.Lined ?? [];
        if (lined.Count > 0)
        {
            // No rule keeps one side's copy of the lines it settles: a writer that fails leaves the conflict, for the session to settle.
            var lineWriters = lined.Select(each => new GeneratedWriters.Writer(each.Path, each.Run)).ToList();
            var lineWritten = await GeneratedWriters.WriteAsync(tree, lineWriters, [.. lined.Select(each => each.Path)], ct, commit: false).ConfigureAwait(false);
            if (lineWritten.Failed is { } failed)
            {
                return new IntegrationResolved { Unsettled = [.. lined.Select(each => each.Path)], Refused = LineRules.Unwritten(lined, failed) };
            }
        }

        return new IntegrationResolved
        {
            Backlog = stops?.Settled, Tables = stops?.Tables ?? [], Taken = taken, Written = written, Lined = lined, Joined = joined,
        };
    }
}
