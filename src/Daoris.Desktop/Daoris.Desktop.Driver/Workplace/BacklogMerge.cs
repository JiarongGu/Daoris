using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The repository's declared backlog and its merge by the merge base (LANDBACKLOG1, D87's note): a landing's replay that conflicts only in
/// the backlog, or in it beside files the repository marks generated (<see cref="GeneratedPaths"/>), settles the backlog as the merge tool
/// settles it (MERGETASKS1): a line one side added is kept, one a side removed is dropped, and a base line both sides changed stays the
/// session's conflict. In this repository nearly every landing touches the backlog, since each session archives its own row, so a
/// refusal there cost the session a rebase turn over a line the merge tool settles by itself.
/// </summary>
/// <remarks>
/// <para><b>A twin</b> of <c>tools/merge-branch.mjs</c>'s <c>backlogOf</c> and <c>mergeByBase</c>: no code is shared (the merge tool is
/// the repository's own Node tooling, this the driver), so both hold one table, the driver's tests' <c>fixtures/backlog-merges.json</c>,
/// row for row (<c>BacklogMergeTests</c>, <c>merge-branch.test.ts</c>), and a change to one is a change to both.</para>
/// <para><b>Which manifest.</b> The merge tool reads the checkout's <c>daoris.json</c>, at the parent's command; a landing reads the
/// line's, at the commit the replay builds on, never the session's copy, so a session cannot declare a file of its own the backlog and
/// have its conflict there settled unseen. A manifest git cannot read, or one that does not read as an object, settles nothing.</para>
/// <para>Nothing here resolves code: a backlog is rows of text a session adds and removes, and its base says which side changed what.
/// Union would undo every row a side moved to the archive, which is why it is not union-merged.</para>
/// </remarks>
internal static class BacklogMerge
{
    /// <summary>The manifest that declares the backlog, at the repository's root.</summary>
    internal const string Manifest = "daoris.json";

    /// <summary>The backlog where the manifest names none, or where there is no manifest.</summary>
    internal const string Default = "TASKS.md";

    /// <summary>A change from the base to a side: the base's lines <c>[Start, End)</c> and what replaced them.</summary>
    internal sealed record Hunk(int Start, int End, IReadOnlyList<string> Lines);

    /// <summary>
    /// The backlog merged: its text, or null where both sides changed one base line (or one added inside a run the other replaced); the
    /// lines kept from the changes taken, the base lines they dropped, and each base line both changed (1-based).
    /// </summary>
    internal sealed record Merged(string? Text, int Kept, int Dropped, IReadOnlyList<int> Both);

    /// <summary>What one replay settled of the backlog, summed over its stops: what the landing says.</summary>
    internal sealed record Settled(string Path, int Kept, int Dropped);

    /// <summary>
    /// The backlog <paramref name="manifest"/>'s text declares: <see cref="Default"/> with no manifest; null where it is not JSON or not an
    /// object, so nothing is settled; <c>documents.backlog</c> as text, or an object's text <c>path</c>, with each backslash a slash; else
    /// <see cref="Default"/>. The merge tool's <c>backlogOf</c>, row for row.
    /// </summary>
    internal static string? PathOf(string? manifest)
    {
        if (manifest is null) return Default;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(manifest);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Object
                || !documents.TryGetProperty("backlog", out var entry))
            {
                return Default;
            }

            var path = entry.ValueKind switch
            {
                JsonValueKind.String => entry.GetString(),
                JsonValueKind.Object when entry.TryGetProperty("path", out var named) && named.ValueKind == JsonValueKind.String => named.GetString(),
                _ => null,
            };
            return path?.Replace('\\', '/') ?? Default;
        }
    }

    /// <summary>
    /// The changes from <paramref name="base"/> to <paramref name="side"/>, as hunks of the base's lines and what replaced them, by the
    /// longest common run of lines: the merge tool's <c>lineHunks</c>, step for step, so a tie is broken alike (an added line first).
    /// </summary>
    internal static IReadOnlyList<Hunk> LineHunks(IReadOnlyList<string> @base, IReadOnlyList<string> side)
    {
        var head = 0;
        while (head < @base.Count && head < side.Count && @base[head] == side[head]) head++;
        var tail = 0;
        while (tail < @base.Count - head && tail < side.Count - head && @base[@base.Count - 1 - tail] == side[side.Count - 1 - tail]) tail++;
        var a = @base.Skip(head).Take(@base.Count - tail - head).ToList();
        var b = side.Skip(head).Take(side.Count - tail - head).ToList();
        // The common run's length from each pair of positions to the end, one row of b per line of a.
        var width = b.Count + 1;
        var run = new int[(a.Count + 1) * width];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                run[i * width + j] = a[i] == b[j] ? run[(i + 1) * width + j + 1] + 1 : Math.Max(run[(i + 1) * width + j], run[i * width + j + 1]);
            }
        }

        var hunks = new List<Hunk>();
        (int Start, int End, List<string> Lines)? open = null;
        var (x, y) = (0, 0);
        while (x < a.Count || y < b.Count)
        {
            if (x < a.Count && y < b.Count && a[x] == b[y])
            {
                Close();
                (x, y) = (x + 1, y + 1);
                continue;
            }

            open ??= (head + x, head + x, []);
            if (y < b.Count && (x == a.Count || run[x * width + y + 1] >= run[(x + 1) * width + y]))
            {
                open.Value.Lines.Add(b[y]);
                y++;
            }
            else
            {
                x++;
                open = open.Value with { End = head + x };
            }
        }

        Close();
        return hunks;

        void Close()
        {
            if (open is { } done) hunks.Add(new Hunk(done.Start, done.End, done.Lines));
            open = null;
        }
    }

    /// <summary>
    /// The backlog merged line by line against its merge base: <paramref name="ours"/> is the side the work lands on (the line in a
    /// replay, <c>main</c> in the merge tool) and its rows go first where both add at one place. The merge tool's <c>mergeByBase</c>, row
    /// for row of <c>backlog-merges.json</c>.
    /// </summary>
    internal static Merged ByBase(string @base, string ours, string theirs)
    {
        var b = @base.Split('\n');
        var mine = LineHunks(b, ours.Split('\n'));
        var yours = LineHunks(b, theirs.Split('\n'));
        var both = new List<int>();
        var hunks = mine.Select(hunk => (Hunk: hunk, Side: 0)).ToList();
        foreach (var hunk in yours)
        {
            if (mine.Any(other => Same(other, hunk))) continue;
            var met = mine.Where(other => Collide(other, hunk)).ToList();
            if (met.Count > 0) both.Add(Math.Min(hunk.Start, met.Min(other => other.Start)) + 1);
            hunks.Add((hunk, 1));
        }

        var kept = hunks.Sum(each => each.Hunk.Lines.Count);
        var dropped = hunks.Sum(each => each.Hunk.End - each.Hunk.Start);
        if (both.Count > 0) return new Merged(null, kept, dropped, both);
        var output = new List<string>();
        var at = 0;
        foreach (var (hunk, _) in hunks.OrderBy(each => each.Hunk.Start).ThenBy(each => each.Hunk.End).ThenBy(each => each.Side))
        {
            if (hunk.Start > at) output.AddRange(b[at..hunk.Start]);
            output.AddRange(hunk.Lines);
            at = hunk.End;
        }

        if (at < b.Length) output.AddRange(b[at..]);
        return new Merged(string.Join('\n', output), kept, dropped, []);

        static bool Same(Hunk x, Hunk y) => x.Start == y.Start && x.End == y.End && x.Lines.SequenceEqual(y.Lines, StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether two sides' hunks touch the same base line, or one adds inside a run the other replaced; adds at one place never do (the
    /// merge tool's <c>hunksCollide</c>).
    /// </summary>
    private static bool Collide(Hunk x, Hunk y)
    {
        if (x.Start == x.End && y.Start == y.End) return false;
        if (x.Start == x.End) return y.Start < x.Start && x.Start < y.End;
        if (y.Start == y.End) return x.Start < y.Start && y.Start < x.End;
        return x.Start < y.End && y.Start < x.End;
    }

    /// <summary>
    /// The backlog the manifest declares as it stands at <paramref name="source"/> (a commit or a tree), read in
    /// <paramref name="place"/>: <see cref="Default"/> where the manifest is not there; null where git cannot say, or the manifest does
    /// not read, so nothing is settled.
    /// </summary>
    internal static async Task<string?> DeclaredAsync(string place, string source, CancellationToken ct)
    {
        var (listed, entry, _) = await WorkingTree.GitAsync(place, ["ls-tree", "-z", source, "--", Manifest], ct).ConfigureAwait(false);
        if (listed != 0) return null;
        var line = entry.TrimEnd('\0');
        if (line.Length == 0) return PathOf(null);
        var tab = line.IndexOf('\t');
        if (tab < 0 || line[..tab].Split(' ') is not [_, "blob", var blob]) return null;
        var (read, text, _) = await WorkingTree.GitAsync(place, ["cat-file", "blob", blob], ct).ConfigureAwait(false);
        return read == 0 ? PathOf(text) : null;
    }

    /// <summary>
    /// A trial's step settled (LANDBACKLOG1 in <see cref="LineHolds.TrialTreeAsync"/>), objects only: the tree git wrote for the step,
    /// <paramref name="written"/>, with <paramref name="path"/> the merge by base of the step's base, the trial's tree so far and the
    /// replayed commit. Null where a side has no such file, git could not say, or both sides changed one base line.
    /// </summary>
    internal static async Task<string?> TrialAsync(
        string root, string path, string @base, string ours, string theirs, string written, CancellationToken ct)
    {
        var texts = new List<string>(3);
        foreach (var side in new[] { @base, ours, theirs })
        {
            var (code, text, _) = await WorkingTree.GitAsync(root, ["cat-file", "blob", $"{side}:{path}"], ct).ConfigureAwait(false);
            if (code != 0) return null;
            texts.Add(text);
        }

        if (ByBase(texts[0], texts[1], texts[2]).Text is not { } merged) return null;
        var (hashed, blob, _) = await WorkingTree.GitAsync(root, ["hash-object", "-w", "--stdin"], merged, ct).ConfigureAwait(false);
        return hashed == 0 ? await WithBlobAsync(root, written, path.Split('/'), 0, blob.Trim(), ct).ConfigureAwait(false) : null;
    }

    /// <summary>
    /// <paramref name="tree"/> with the blob at <paramref name="segments"/> from <paramref name="at"/> on replaced by <paramref name="blob"/>,
    /// each tree above it written again (<c>git mktree</c>), its mode kept; null where the path is not a file there or git could not say.
    /// </summary>
    private static async Task<string?> WithBlobAsync(string root, string tree, string[] segments, int at, string blob, CancellationToken ct)
    {
        var (listed, output, _) = await WorkingTree.GitAsync(root, ["ls-tree", "-z", tree], ct).ConfigureAwait(false);
        if (listed != 0) return null;
        var entries = output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = entries.FindIndex(each => each.IndexOf('\t') is var tab and >= 0 && each[(tab + 1)..] == segments[at]);
        if (index < 0) return null;
        var entry = entries[index];
        if (entry[..entry.IndexOf('\t')].Split(' ') is not [var mode, var type, var sha]) return null;
        var last = at == segments.Length - 1;
        if (type != (last ? "blob" : "tree")) return null;
        var now = last ? blob : await WithBlobAsync(root, sha, segments, at + 1, blob, ct).ConfigureAwait(false);
        if (now is null) return null;
        entries[index] = $"{mode} {type} {now}\t{segments[at]}";
        var (made, written, _) = await WorkingTree.GitAsync(root, ["mktree", "-z"], string.Join('\0', entries) + "\0", ct).ConfigureAwait(false);
        return made == 0 && written.Trim() is { Length: > 0 } rewritten ? rewritten : null;
    }

    /// <summary>
    /// The backlog's settling over one landing's replay (LANDBACKLOG1 in <see cref="BranchReplays"/>): the path the line declares at
    /// <paramref name="source"/>, read once, at the first stop that needs it, and what each stop settled, summed.
    /// </summary>
    internal sealed class Stops(string source)
    {
        private Task<string?>? _declared;

        /// <summary>What the replay settled; null where no stop settled the backlog.</summary>
        internal Settled? Settled { get; private set; }

        /// <summary>The backlog the line declares, read in <paramref name="tree"/> once per replay.</summary>
        internal Task<string?> DeclaredAsync(string tree, CancellationToken ct) => _declared ??= BacklogMerge.DeclaredAsync(tree, source, ct);

        /// <summary>
        /// The backlog's conflict at a replay's stop in <paramref name="tree"/> merged by its base and staged, the working tree written from
        /// the index; false where a stage is missing (a side added or deleted it), git could not say, or both sides changed one base line,
        /// and the stop stays the session's conflict.
        /// </summary>
        /// <remarks>
        /// The merged text is hashed as given and put in the index by its blob, never added from a file: the person's <c>core.autocrlf</c>
        /// and <c>core.safecrlf</c> would otherwise convert it or refuse it. The checkout after writes it out through their filters.
        /// </remarks>
        internal async Task<bool> SettleAsync(string tree, string path, CancellationToken ct)
        {
            var (listed, output, _) = await WorkingTree.GitAsync(tree, ["ls-files", "-s", "-z", "--", path], ct).ConfigureAwait(false);
            if (listed != 0) return false;
            // `<mode> <blob> <stage>\t<path>` for each stage git holds.
            var stages = new Dictionary<string, (string Mode, string Blob)>(StringComparer.Ordinal);
            foreach (var each in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var tab = each.IndexOf('\t');
                if (tab >= 0 && each[..tab].Split(' ') is [var mode, var blob, var stage]) stages[stage] = (mode, blob);
            }

            if (!stages.ContainsKey("1") || !stages.ContainsKey("2") || !stages.ContainsKey("3")) return false;
            var texts = new List<string>(3);
            foreach (var stage in new[] { "1", "2", "3" })
            {
                var (read, text, _) = await WorkingTree.GitAsync(tree, ["cat-file", "blob", stages[stage].Blob], ct).ConfigureAwait(false);
                if (read != 0) return false;
                texts.Add(text);
            }

            var merged = ByBase(texts[0], texts[1], texts[2]);
            if (merged.Text is null) return false;
            var (hashed, made, _) = await WorkingTree.GitAsync(tree, ["hash-object", "-w", "--stdin"], merged.Text, ct).ConfigureAwait(false);
            if (hashed != 0) return false;
            var (staged, _, _) = await WorkingTree.GitAsync(
                tree, ["update-index", "--cacheinfo", $"{stages["2"].Mode},{made.Trim()},{path}"], ct).ConfigureAwait(false);
            if (staged != 0) return false;
            var (written, _, _) = await WorkingTree.GitAsync(tree, ["checkout", "--", path], ct).ConfigureAwait(false);
            if (written != 0) return false;
            Settled = new Settled(path, (Settled?.Kept ?? 0) + merged.Kept, (Settled?.Dropped ?? 0) + merged.Dropped);
            return true;
        }
    }
}
