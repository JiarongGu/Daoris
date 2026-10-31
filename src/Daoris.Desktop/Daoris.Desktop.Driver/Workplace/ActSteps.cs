using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>An index entry as git lists it (<c>ls-files -s</c>): its mode, its object, its stage and its path.</summary>
public sealed record ActEntry(string Mode, string Id, int Stage, string Path);

/// <summary>How a step's effect ended: its exit, or that its processes were not seen to end, what it said, and whether the act stops after it.</summary>
internal sealed record ActStepEnd(int? Exit, bool Unended, string Said, bool Stops);

/// <summary>
/// The three effect steps an act takes (GITAPP2a, D204; the git-writes design §5, §6): their plans as the journal keeps them, their effects
/// under the checkout lock, and their postconditions, which alone say what an effect did.
/// </summary>
/// <remarks>
/// <para><b>A ref step</b> is one <c>update-ref --stdin</c> transaction (<see cref="RefTransaction"/>'s input), its git started suspended in the
/// act's job (<see cref="ActLaunch"/>), so its <c>reference-transaction</c> hook and the hook's children are ended with it.</para>
/// <para><b>A configuration step and an index step move under git's own lock</b>: Daoris makes <c>config.lock</c> or <c>index.lock</c> exclusively,
/// as git does, compares the journaled section (its file and scope; one set in another scope refuses) or entries, and on a match writes the
/// replacement into the lock and renames it over the file; the index's replacement is built on a copy by <c>update-index --index-info</c> in the
/// act's job. A lock that stands is refused and named, never removed; a mismatch removes only Daoris's own lock and refuses, the recovery kept.</para>
/// </remarks>
internal static class ActSteps
{
    /// <summary>A ref step: <paramref name="transaction"/>'s input, each moved ref's value before and after for its postconditions and its undo.</summary>
    /// <exception cref="ArgumentException">The transaction is refused before git.</exception>
    public static ActStepPlan Ref(RefTransaction transaction, IEnumerable<(string Ref, string? Before, string? After)> moves)
    {
        var (input, refusal) = transaction.Build();
        if (refusal is not null) throw new ArgumentException(refusal);
        var refs = new JsonArray([.. moves.Select(move => (JsonNode)new JsonObject { ["ref"] = move.Ref, ["before"] = move.Before, ["after"] = move.After })]);
        var undo = new JsonArray([.. moves.Select(move => (JsonNode)new JsonObject { ["ref"] = move.Ref, ["before"] = move.After, ["after"] = move.Before })]);
        return new ActStepPlan(ActStepKinds.Ref, new JsonObject { ["input"] = input }.ToJsonString(),
            new JsonObject { ["refs"] = refs }.ToJsonString(), new JsonObject { ["refs"] = undo }.ToJsonString());
    }

    /// <summary>A configuration step: <paramref name="section"/> of <paramref name="file"/>'s <paramref name="scope"/> as read, moved to <paramref name="to"/> or, where null, removed.</summary>
    public static ActStepPlan Config(string file, string scope, string section, string? to, string text)
    {
        var said = new JsonObject { ["file"] = file, ["scope"] = scope, ["section"] = section, ["to"] = to, ["text"] = text }.ToJsonString();
        var undo = to is null ? null : new JsonObject { ["file"] = file, ["scope"] = scope, ["section"] = to, ["to"] = section, ["text"] = ActConfig.Renamed(text, to) }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Config, said, said, undo);
    }

    /// <summary>A configuration step that puts <paramref name="text"/> back as the section <paramref name="section"/>, only where none stands (a deleted branch's, returned).</summary>
    public static ActStepPlan Restore(string file, string scope, string section, string text)
    {
        var said = new JsonObject { ["file"] = file, ["scope"] = scope, ["section"] = section, ["to"] = null, ["text"] = text, ["restore"] = true }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Config, said, said);
    }

    /// <summary>An index step in the tree whose git directory is <paramref name="gitDir"/>: the entries as read, and what replaces them (an entry absent there is removed).</summary>
    public static ActStepPlan Index(string gitDir, IReadOnlyList<ActEntry> before, IReadOnlyList<ActEntry> after)
    {
        var said = new JsonObject { ["gitDir"] = gitDir, ["before"] = Entries(before), ["after"] = Entries(after) }.ToJsonString();
        var undo = new JsonObject { ["gitDir"] = gitDir, ["before"] = Entries(after), ["after"] = Entries(before) }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Index, said, said, undo);
    }

    /// <summary>The bound a step of <paramref name="kind"/> runs under, by the git it is or starts (<see cref="GitBounds"/>).</summary>
    public static TimeSpan BoundOf(string kind) => GitBounds.Of([kind == ActStepKinds.Index ? "update-index" : "update-ref"]);

    // ——— The effects, under the checkout's hold.

    /// <summary>The step's effect, its start already written: run once, never again by a reconciler.</summary>
    public static Task<ActStepEnd> EffectAsync(TreeLock held, ActLaunch launch, ActStep step) => step.Kind switch
    {
        ActStepKinds.Ref => RefAsync(held, launch, step),
        ActStepKinds.Config => ConfigAsync(held, launch, step),
        _ => IndexAsync(held, launch, step),
    };

    private static async Task<ActStepEnd> RefAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var input = Read(step.Arguments)["input"]!.GetValue<string>();
        // A recovery ref is dated by its reflog's first entry (RECOVERYDATE1), which git writes outside refs/heads/ only when asked
        // (`--create-reflog` has no place in the census's literal command, so the setting is passed for this one git).
        var reflog = input.Contains(ContentHolds.DiscardedPrefix, StringComparison.Ordinal)
            ? new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_CONFIG_COUNT"] = "1", ["GIT_CONFIG_KEY_0"] = "core.logAllRefUpdates", ["GIT_CONFIG_VALUE_0"] = "always" }
            : null;
        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, ["update-ref", "--stdin"], input, reflog).ConfigureAwait(false);
        return run.End(run.Exit == 0 ? "git made the transaction" : $"git refused the transaction ({ContentHolds.Said(run.Stderr)})");
    }

    private static async Task<ActStepEnd> ConfigAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = Read(step.Arguments);
        var (file, scope, section, to, text) = (Text(plan, "file")!, Text(plan, "scope")!, Text(plan, "section")!, Text(plan, "to"), Text(plan, "text")!);
        var restore = plan["restore"] is JsonValue flag && flag.TryGetValue<bool>(out var on) && on;
        if (!restore && await ElsewhereAsync(launch.Root, section, scope).ConfigureAwait(false) is { } elsewhere) return Refused(elsewhere);

        var lockPath = file + ".lock";
        if (Exclusive(lockPath) is not { } exclusive) return Refused(ActLocks.Stale(lockPath, launch.Repository));
        try
        {
            var now = ActConfig.Decode(await File.ReadAllBytesAsync(file).ConfigureAwait(false));
            if (now is null) return Mine(lockPath, exclusive, $"`{file}` does not read as UTF-8, so the `{section}` section was not moved");
            string replaced;
            if (restore)
            {
                if (ActConfig.Find(now, section) is not null) return Mine(lockPath, exclusive, $"a `{section}` section stands, so it was not restored");
                replaced = (now.Length == 0 || now.EndsWith('\n') ? now : now + "\n") + (text.EndsWith('\n') ? text : text + "\n");
            }
            else
            {
                if (ActConfig.Find(now, section) is not { } found || found.Text != text)
                {
                    return Mine(lockPath, exclusive, $"the `{section}` section changed since it was read, so nothing was moved; the recovery is kept");
                }

                if (to is not null && ActConfig.Find(now, to) is not null) return Mine(lockPath, exclusive, $"a `{to}` section stands, so `{section}` was not moved onto it");
                replaced = now[..found.Start] + (to is null ? "" : ActConfig.Renamed(text, to)) + now[(found.Start + found.Text.Length)..];
            }

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(replaced);
            await exclusive.WriteAsync(bytes).ConfigureAwait(false);
            exclusive.Flush(flushToDisk: true);
            exclusive.Dispose();
            File.Move(lockPath, file, overwrite: true);
            return new ActStepEnd(0, false, restore ? $"the `{section}` section was restored" : to is null ? $"the `{section}` section was removed" : $"the `{section}` section was moved to `{to}`", false);
        }
        catch
        {
            Let(lockPath, exclusive);
            throw;
        }
    }

    private static async Task<ActStepEnd> IndexAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = Read(step.Arguments);
        var gitDir = Text(plan, "gitDir")!;
        var (before, after) = (EntriesOf(plan["before"]), EntriesOf(plan["after"]));
        var paths = before.Concat(after).Select(entry => entry.Path).Distinct(StringComparer.Ordinal).ToList();
        var index = Path.Combine(gitDir, "index");
        var lockPath = index + ".lock";
        if (Exclusive(lockPath) is not { } exclusive) return Refused(ActLocks.Stale(lockPath, launch.Repository));
        var copy = Path.Combine(gitDir, $"index.daoris-{step.Act}");
        try
        {
            var current = await ActIndex.EntriesAsync(launch.Root, paths, null).ConfigureAwait(false);
            if (current is null) return Mine(lockPath, exclusive, "git could not list the index's entries, so none was replaced");
            if (!ActIndex.Same(current, before)) return Mine(lockPath, exclusive, "the index's entries changed since they were read, so none was replaced; the recovery is kept");

            File.Copy(index, copy, overwrite: true);
            var input = new StringBuilder();
            foreach (var entry in after) input.Append($"{entry.Mode} {entry.Id} {entry.Stage}\t{entry.Path}\n");
            foreach (var gone in before.Where(entry => !after.Any(each => each.Path == entry.Path && each.Stage == entry.Stage)))
            {
                input.Append($"0 {new string('0', gone.Id.Length)} {gone.Stage}\t{gone.Path}\n");
            }

            var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_INDEX_FILE"] = copy };
            var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, ["update-index", "--index-info"], input.ToString(), environment).ConfigureAwait(false);
            if (run.Exit != 0 || run.Unended)
            {
                Gone(copy);
                Let(lockPath, exclusive);
                return run.End($"the replacement index was not built ({(run.Exit is null ? run.Said : ContentHolds.Said(run.Stderr))}), so none was replaced", stops: true);
            }

            var built = await ActIndex.EntriesAsync(launch.Root, paths, copy).ConfigureAwait(false);
            if (built is null || !ActIndex.Same(built, after))
            {
                Gone(copy);
                return Mine(lockPath, exclusive, "the replacement index did not hold the entries asked for, so none was replaced");
            }

            await exclusive.WriteAsync(await File.ReadAllBytesAsync(copy).ConfigureAwait(false)).ConfigureAwait(false);
            exclusive.Flush(flushToDisk: true);
            exclusive.Dispose();
            File.Move(lockPath, index, overwrite: true);
            Gone(copy);
            return new ActStepEnd(0, false, $"{after.Count} index entr{(after.Count == 1 ? "y was" : "ies were")} replaced", false);
        }
        catch
        {
            Gone(copy);
            Let(lockPath, exclusive);
            throw;
        }
    }

    /// <summary>Where the section's keys are set in another scope than the one journaled: refused, named; null where none is.</summary>
    private static async Task<string?> ElsewhereAsync(string root, string section, string scope)
    {
        var (code, listed, err) = await WorkingTree.GitAsync(
            root, ["config", "--show-scope", "--name-only", "--get-regexp", "^" + Regex.Escape(section) + "\\."], environment: null, GitBounds.Question,
            CancellationToken.None).ConfigureAwait(false);
        if (code == 1) return null;
        if (code != 0) return $"git could not say where the `{section}` section is set ({ContentHolds.Said(err)}), so it was not moved";
        var other = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split('\t')[0].Trim()).FirstOrDefault(each => each != scope);
        return other is null ? null : $"the `{section}` section is set in the {other} configuration too, so it was not moved";
    }

    /// <summary>The lock made exclusively, as git makes it; null where it stands.</summary>
    private static FileStream? Exclusive(string lockPath)
    {
        try
        {
            return new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(lockPath))
        {
            return null;
        }
    }

    /// <summary>Daoris's own lock removed, and the step refused in <paramref name="said"/>.</summary>
    private static ActStepEnd Mine(string lockPath, FileStream exclusive, string said)
    {
        Let(lockPath, exclusive);
        return Refused(said);
    }

    private static void Let(string lockPath, FileStream exclusive)
    {
        if (!exclusive.SafeFileHandle.IsClosed)
        {
            exclusive.Dispose();
            Gone(lockPath);
        }
    }

    private static void Gone(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A copy left beside the index is Daoris's own scratch, named for its act.
        }
    }

    private static ActStepEnd Refused(string said) => new(null, false, said, true);

    // ——— The postconditions, read after the effect and never from an exit code.

    /// <summary>What the step's postconditions read now: <see cref="ActOutcomes.Done"/>, <see cref="ActOutcomes.NotDone"/> or <see cref="ActOutcomes.Unknown"/>; null where git or the file could not say.</summary>
    public static async Task<string?> ReadAsync(string root, ActStep step, CancellationToken ct)
    {
        var plan = Read(step.Postconditions);
        switch (step.Kind)
        {
            case ActStepKinds.Ref:
            {
                var moves = plan["refs"]!.AsArray().Select(each => (Ref: Text(each!, "ref")!, Before: Text(each!, "before"), After: Text(each!, "after"))).ToList();
                if (moves.Count == 0) return ActOutcomes.Done;
                var (code, listed, _) = await WorkingTree.GitAsync(
                    root, ["for-each-ref", "--format=%(refname)%09%(objectname)", "--", .. moves.Select(move => move.Ref)], environment: null, GitBounds.Question, ct)
                    .ConfigureAwait(false);
                if (code != 0) return null;
                var stands = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r').Split('\t'))
                    .Where(parts => parts.Length == 2).ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
                return Whole(moves.Select(move => stands.GetValueOrDefault(move.Ref) is var now && now == move.After ? ActOutcomes.Done
                    : now == move.Before ? ActOutcomes.NotDone : ActOutcomes.Unknown));
            }

            case ActStepKinds.Config:
            {
                var (file, section, to, text) = (Text(plan, "file")!, Text(plan, "section")!, Text(plan, "to"), Text(plan, "text")!);
                string? now;
                try
                {
                    now = ActConfig.Decode(await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return null;
                }

                if (now is null) return null;
                var (source, target) = (ActConfig.Find(now, section), to is null ? null : ActConfig.Find(now, to));
                if (plan["restore"] is JsonValue flag && flag.TryGetValue<bool>(out var on) && on)
                {
                    return source is null ? ActOutcomes.NotDone : source.Text.TrimEnd() == text.TrimEnd() ? ActOutcomes.Done : ActOutcomes.Unknown;
                }

                if (source is null && (to is null || target?.Text == ActConfig.Renamed(text, to))) return ActOutcomes.Done;
                // A removal asked and the section stands, whatever it now says: it was not removed.
                if (to is null && source is not null) return ActOutcomes.NotDone;
                return source?.Text == text && target is null ? ActOutcomes.NotDone : ActOutcomes.Unknown;
            }

            default:
            {
                var (before, after) = (EntriesOf(plan["before"]), EntriesOf(plan["after"]));
                var paths = before.Concat(after).Select(entry => entry.Path).Distinct(StringComparer.Ordinal).ToList();
                var current = await ActIndex.EntriesAsync(root, paths, null).ConfigureAwait(false);
                if (current is null) return null;
                return ActIndex.Same(current, after) ? ActOutcomes.Done : ActIndex.Same(current, before) ? ActOutcomes.NotDone : ActOutcomes.Unknown;
            }
        }
    }

    /// <summary>One outcome of many: all done, all not done, or else unknown.</summary>
    private static string Whole(IEnumerable<string> outcomes)
    {
        var all = outcomes.Distinct().ToList();
        return all.Count == 1 ? all[0] : ActOutcomes.Unknown;
    }

    // ——— The plans' JSON.

    private static JsonObject Read(string json) => JsonNode.Parse(json)?.AsObject() ?? throw new DriverException("an act's step does not read as an object");

    private static string? Text(JsonNode node, string name) => node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static JsonArray Entries(IEnumerable<ActEntry> entries) =>
        new([.. entries.Select(entry => (JsonNode)new JsonObject { ["mode"] = entry.Mode, ["id"] = entry.Id, ["stage"] = entry.Stage, ["path"] = entry.Path })]);

    internal static IReadOnlyList<ActEntry> EntriesOf(JsonNode? node) =>
        node is JsonArray array
            ? [.. array.Select(each => new ActEntry(Text(each!, "mode")!, Text(each!, "id")!, each!["stage"]!.GetValue<int>(), Text(each!, "path")!))]
            : [];
}

/// <summary>An effect step's git, started suspended in its act's job, its id journaled before it runs, bounded, its tree ended past its bound.</summary>
internal sealed class ActLaunch(ActJournal journal, ActClaim claim, ProcessJob job, string root, string repository, ActRunsLimits limits)
{
    public string Root => root;

    public string Repository => repository;

    /// <summary>What one held git did: its exit, its words, and whether it outlived its bound or its end.</summary>
    internal sealed record Ran(int? Exit, string Stdout, string Stderr, bool Unended, string Said)
    {
        public ActStepEnd End(string said, bool stops = false) => new(Exit, Unended, Unended ? Said : Exit is null ? Said : said, stops || Exit != 0 || Unended);
    }

    /// <summary>
    /// git with <paramref name="arguments"/> in <paramref name="cwd"/>, started suspended and joined to the act's job; its id and start written;
    /// let go; then <paramref name="input"/> written and its exit awaited until the step's deadline. Past it, the job's processes are ended and
    /// awaited empty, else said unended. Counted at the one seam (<see cref="GitLaunch"/>).
    /// </summary>
    public Task<Ran> HeldGitAsync(string cwd, int ordinal, IReadOnlyList<string> arguments, string? input, IReadOnlyDictionary<string, string>? environment) =>
        GitLaunch.LaunchAsync(GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Held, async () =>
        {
            var (info, refusal) = WorkingTree.GitStart(cwd, arguments, journal.Home);
            if (info is null) return new Ran(null, "", "", false, $"git could not be started ({refusal}), so the step did not run");
            info.RedirectStandardInput = true;
            if (environment is not null)
            {
                foreach (var (name, value) in environment) info.Environment[name] = value;
            }

            HeldChild child;
            try
            {
                child = HeldChild.Start(job, info, input is not null);
            }
            catch (DriverException error)
            {
                return new Ran(null, "", "", false, error.Message);
            }

            try
            {
                if (limits.Seam is { } beforePid) await beforePid(ActSeams.BeforePid, ordinal).ConfigureAwait(false);
                journal.Launched(claim, ordinal, child.Pid, child.Started);
                child.Resume();
                var writing = child.WriteAsync(input);
                if (limits.Seam is { } afterLaunch) await afterLaunch(ActSeams.AfterLaunch, ordinal).ConfigureAwait(false);
                var step = journal.Steps(claim.Act).Single(each => each.Ordinal == ordinal);
                var left = (step.Deadline ?? DateTimeOffset.UtcNow) - journal.Clock.GetUtcNow();
                var (exit, stdout, stderr) = await child.ExitAsync(left > TimeSpan.Zero ? left : TimeSpan.Zero).ConfigureAwait(false);
                if (exit is not null)
                {
                    await writing.ConfigureAwait(false);
                    return new Ran(exit, stdout, stderr, false, "");
                }

                (limits.Ending ?? (each => each.Terminate()))(job);
                var emptied = await job.WaitEmptyAsync(limits.ExitBound).ConfigureAwait(false);
                return emptied
                    ? new Ran(null, "", "", false, $"git did not answer within its bound, and its processes were ended; its postconditions decide")
                    : new Ran(null, "", "", true, $"git did not answer within its bound, and its processes (pid {child.Pid}) did not end when stopped");
            }
            finally
            {
                child.Dispose();
                RefSnapshot.Ran(arguments);
                GitFacts.Ran(arguments);
            }
        });
}

/// <summary>The index's entries for some paths, as <c>ls-files -s -z</c> lists them, from the index or another index file.</summary>
internal static class ActIndex
{
    /// <summary>The entries of <paramref name="paths"/> in the index, or in <paramref name="file"/> where given; null where git could not list them.</summary>
    public static async Task<IReadOnlyList<ActEntry>?> EntriesAsync(string root, IReadOnlyList<string> paths, string? file)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (file is not null) environment["GIT_INDEX_FILE"] = file;
        var (code, listed, _) = await WorkingTree.GitAsync(
            root, ["ls-files", "-s", "-z", "--full-name", "--", .. paths.Select(path => ":(literal)" + path)], environment, GitBounds.Question, CancellationToken.None)
            .ConfigureAwait(false);
        if (code != 0) return null;
        var entries = new List<ActEntry>();
        foreach (var row in listed.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = row.IndexOf('\t');
            var head = tab < 0 ? [] : row[..tab].Split(' ');
            if (head.Length != 3 || !int.TryParse(head[2], out var stage)) return null;
            entries.Add(new ActEntry(head[0], head[1], stage, row[(tab + 1)..]));
        }

        return entries;
    }

    /// <summary>Whether two lists hold the same entries, in any order.</summary>
    public static bool Same(IReadOnlyList<ActEntry> left, IReadOnlyList<ActEntry> right) =>
        left.Count == right.Count && left.All(right.Contains);
}

/// <summary>A configuration file's sections, read and moved as text: git's own headers, <c>[name]</c> and <c>[name "sub"]</c>.</summary>
internal static partial class ActConfig
{
    /// <summary>A section's text as it stands in a file, from its header to the next, and where it starts.</summary>
    internal sealed record Section(int Start, string Text);

    [GeneratedRegex("""^[ \t]*\[(?<name>[^\]\s"]+)(?:[ \t]+"(?<sub>(?:[^"\\]|\\.)*)")?[ \t]*\]""")]
    private static partial Regex Header();

    /// <summary>The section <paramref name="section"/> (<c>name</c> or <c>name.sub</c>) of the configuration file at <paramref name="file"/>; null where it has none.</summary>
    public static async Task<string?> SectionAsync(string file, string section) =>
        Decode(await File.ReadAllBytesAsync(file).ConfigureAwait(false)) is { } text ? Find(text, section)?.Text : null;

    /// <summary>The bytes as UTF-8, or null where they do not read as it.</summary>
    public static string? Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>The first section named <paramref name="section"/> in <paramref name="text"/>, its name blind to case and its subsection exact.</summary>
    public static Section? Find(string text, string section)
    {
        var lines = Lines(text);
        for (var at = 0; at < lines.Count; at++)
        {
            if (!Names(lines[at].Text, section)) continue;
            var end = lines.Skip(at + 1).FirstOrDefault(line => Header().IsMatch(line.Text));
            var stop = end.Text is null ? text.Length : end.Start;
            return new Section(lines[at].Start, text[lines[at].Start..stop]);
        }

        return null;
    }

    /// <summary>The section's text with its header renamed to <paramref name="to"/>, everything after the header as it was.</summary>
    public static string Renamed(string text, string to)
    {
        var header = Header().Match(text);
        var dot = to.IndexOf('.');
        var named = dot < 0 ? $"[{to}]" : $"[{to[..dot]} \"{to[(dot + 1)..].Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]";
        return header.Success ? text[..header.Index] + named + text[(header.Index + header.Length)..] : text;
    }

    /// <summary>Whether the line is the header of <paramref name="section"/>: <c>[core]</c>, the old <c>[branch.topic]</c>, or <c>[branch "topic"]</c>.</summary>
    private static bool Names(string line, string section)
    {
        var header = Header().Match(line);
        if (!header.Success) return false;
        var name = header.Groups["name"].Value;
        if (!header.Groups["sub"].Success) return string.Equals(name, section, StringComparison.OrdinalIgnoreCase);
        var dot = section.IndexOf('.');
        return dot > 0 && string.Equals(name, section[..dot], StringComparison.OrdinalIgnoreCase)
            && Regex.Replace(header.Groups["sub"].Value, @"\\(.)", "$1") == section[(dot + 1)..];
    }

    private static List<(int Start, string Text)> Lines(string text)
    {
        var lines = new List<(int, string)>();
        for (var start = 0; start < text.Length;)
        {
            var end = text.IndexOf('\n', start);
            var stop = end < 0 ? text.Length : end + 1;
            lines.Add((start, text[start..stop]));
            start = stop;
        }

        return lines;
    }
}

/// <summary>git's own lock files, which Daoris refuses beside and names, never removes (the git-writes design §3).</summary>
internal static class ActLocks
{
    /// <summary>A lock that stands, named, with the command to copy once no git runs there.</summary>
    public static string Stale(string lockPath, string repository) =>
        $"git's lock `{lockPath}` stands: a git is running in `{repository}`, or one that ended left it. Daoris never removes it; once no git runs "
        + $"there, remove it and try again: {(OperatingSystem.IsWindows() ? $"del \"{lockPath}\"" : $"rm '{lockPath}'")}";

    /// <summary>
    /// The lock files that stand for the checkout whose git directory is <paramref name="gitDir"/> and common directory <paramref name="commonDir"/>:
    /// the index's and HEAD's, the configuration's, the packed refs', and each named ref's.
    /// </summary>
    public static IReadOnlyList<string> Standing(string gitDir, string commonDir, IEnumerable<string> refs) =>
    [
        .. new[] { Path.Combine(gitDir, "index.lock"), Path.Combine(gitDir, "HEAD.lock"), Path.Combine(commonDir, "config.lock"), Path.Combine(commonDir, "packed-refs.lock") }
            .Concat(refs.Select(reference => Path.Combine(commonDir, reference.Replace('/', Path.DirectorySeparatorChar) + ".lock")))
            .Where(File.Exists),
    ];
}
