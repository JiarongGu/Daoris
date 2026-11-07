using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>What the files stage did in a tree: each line it says, whether a terminal's visit stopped it, and what the session is told.</summary>
internal sealed record TreeFilesOutcome(IReadOnlyList<Noted> Said, bool Stopped, string? Told)
{
    internal static readonly TreeFilesOutcome None = new([], false, null);

    internal Noted? Line() => Said.Count == 0 ? null : Said.Skip(1).Aggregate(Said[0], (all, each) => all.Then(" ", each));
}

/// <summary>
/// The files stage (the tree-pool design §29.7), after a start's record opens and before anything is handed the tree. In an unworked
/// tree, one Daoris marked as it made it and has handed to no agent or terminal since: what a cut-off start left is written back, then
/// each dependency in rule order is kept where its key and record hold, placed from the store at a hit, or installed by Daoris and
/// sealed where its judgement holds; the <c>post-checkout</c> hook the making held runs; and the machine-local files are copied. In a
/// worked tree it installs, removes and runs nothing but a key's <c>toolchain</c> reads: a dependency is kept where its record holds, or
/// placed from a hit into an absent folder where every one before it was kept or placed here; every other is the line's own, said.
/// </summary>
/// <remarks>
/// The mark is read again under the record's lock before each write-back, removal, placement record, hook and copy, and the lock is held
/// through each write, never while an install or the hook runs: once a terminal's visit has taken it, the stage stops, takes its
/// <c>installing</c> mark away and leaves what it had not finished as the line's own.
/// </remarks>
internal sealed class TreeFilesStage(string home, string root, string tree, string repository)
{
    private const string Zeros = "0000000000000000000000000000000000000000";

    private readonly List<Noted> _said = [];
    private readonly List<string> _ready = [];
    private bool _stopped;

    internal async Task<TreeFilesOutcome> RunAsync(CancellationToken ct)
    {
        var head = await WorkingTree.HeadAsync(tree, ct).ConfigureAwait(false);
        if (head is null) return TreeFilesOutcome.None;
        var state = TreeFilesRecord.Read(home, tree);
        var unworked = state is { Unworked: true };
        var deps = await StoreDependencyRules.ReadAsync(home, tree, repository, head, ct).ConfigureAwait(false);
        if (deps.Rules.Unread is { } unread) _said.Add(Noted.Of(StartNoteCodes.StartedFilesUnread, $"this machine's file rules for `{repository}` do not read ({unread}), so Daoris held every install and copy they could restrict here.", ("repository", repository), ("why", unread)));

        if (unworked && !await WriteBackAsync(ct).ConfigureAwait(false)) return Stop();
        var keys = new List<string>();
        var waiting = 0;
        var standing = true;
        foreach (var dependency in deps.Each)
        {
            var key = await DependencyKeys.KeyAsync(home, tree, head, repository, dependency, keys, ct).ConfigureAwait(false);
            keys.Add(key);
            if (unworked)
            {
                var held = await UnworkedAsync(dependency, key, deps.Rules, ct).ConfigureAwait(false);
                if (_stopped) return Stop();
                if (held) waiting++;
            }
            else
            {
                standing = await WorkedAsync(dependency, key, standing, ct).ConfigureAwait(false);
            }
        }

        if (waiting > 0) _said.Add(Noted.Of(StartNoteCodes.StartedFilesWaiting, $"{waiting} of Daoris's acts on files git does not manage in `{repository}` wait for an allow under a restriction set for it, so this tree was not handed what they give.", ("count", waiting), ("repository", repository)));
        if (unworked)
        {
            if (!await HookAsync(head, ct).ConfigureAwait(false)) return Stop();
            if (!TreeFilesRecord.Holding(home, tree, () => { })) return Stop();
            var copied = await TreeLocalInputs.CopyAsync(root, tree, ct).ConfigureAwait(false);
            if (copied.Line() is { } line) _said.Add(line);
            foreach (var each in copied.Declined) _said.Add(each.Line());
        }

        return Outcome();
    }

    /// <summary>Every tracked change and every path git does not ignore, which only a cut-off start's own install or hook can have left, put back.</summary>
    private async Task<bool> WriteBackAsync(CancellationToken ct)
    {
        var changed = await FillJudgement.StatusAsync(tree, ignored: false, ct).ConfigureAwait(false);
        if (changed.Count == 0) return true;
        var wrote = TreeFilesRecord.Holding(home, tree, () => PutBack(changed));
        if (!wrote) return false;
        TreeFilesRecord.WhileUnworked(home, tree, record => record with { HookOwed = true });
        _said.Add(Noted.Of(StartNoteCodes.StartedFilesWrittenBack, $"Daoris put back what a start cut off before it left changed here: {string.Join(", ", changed.Keys.Select(path => $"`{path}`"))}.", ("paths", (IReadOnlyList<string>)[.. changed.Keys])));
        return true;
    }

    private void PutBack(IReadOnlyDictionary<string, string> changed)
    {
        var tracked = changed.Where(each => each.Value != "??").Select(each => each.Key).ToList();
        if (tracked.Count > 0)
        {
            var (code, _, error) = WorkingTree.GitAsync(tree, ["checkout", "HEAD", "--", .. tracked], CancellationToken.None).GetAwaiter().GetResult();
            if (code != 0) throw new DriverException($"git could not put back what an install changed: {error.Trim()}");
        }

        foreach (var path in changed.Where(each => each.Value == "??").Select(each => Path.Combine(tree, each.Key)))
        {
            if (Directory.Exists(path)) TreeSetAside.DeleteNeverFollowing(path);
            else if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>One dependency in an unworked tree; true where a restriction holds its install for an allow.</summary>
    private async Task<bool> UnworkedAsync(StoreDependency dependency, string key, FileRules rules, CancellationToken ct)
    {
        var folder = Path.Combine(tree, dependency.Folder);
        var placement = TreeFilesRecord.Read(home, tree)?.Of(dependency.Path);
        var exists = Path.Exists(folder);
        if (placement is { Installing: true })
        {
            if (!Remove(dependency)) return false;
            (placement, exists) = (null, false);
        }

        if (placement is not null && exists)
        {
            if (placement.Manifest?.Differs(tree, dependency.Folder, dependency.Caches) is { } changed)
            {
                Left(dependency, $"its work changed it: {changed}");
                return false;
            }

            if (placement.Key == key)
            {
                Kept(dependency);
                return false;
            }

            if (!Remove(dependency)) return false;
        }
        else if (exists)
        {
            Left(dependency, "Daoris did not place it");
            return false;
        }

        if (await PlaceAsync(dependency, key, ct).ConfigureAwait(false)) return false;
        if (_stopped) return false;
        var install = dependency.Rule.Install!;
        switch (rules.Judge(FileActs.Run(install)))
        {
            case FileVerdict.Denied:
                Unplaced(dependency, $"a restriction set for it denies `{install}`");
                return false;
            case FileVerdict.Held:
                Unplaced(dependency, $"`{install}` waits for an allow");
                return true;
        }

        await InstallAsync(dependency, key, ct).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// One dependency in a worked tree: kept where its key and record hold, or placed from a hit into an absent folder where no tracked
    /// input of its key has a working change and every dependency before it was kept or placed here. Whether it stands as its key says.
    /// </summary>
    private async Task<bool> WorkedAsync(StoreDependency dependency, string key, bool standing, CancellationToken ct)
    {
        var record = TreeFilesRecord.Read(home, tree);
        var placement = record?.Of(dependency.Path);
        var exists = Path.Exists(Path.Combine(tree, dependency.Folder));
        if (placement is { Installing: false } && exists && placement.Key == key && placement.Manifest?.Differs(tree, dependency.Folder, dependency.Caches) is null)
        {
            Kept(dependency);
            return standing;
        }

        string why;
        if (exists) why = placement is null ? "Daoris did not place it" : placement.Key != key ? "the files that key it moved since it was installed, and this tree is its line's to install in" : "its work changed it";
        else if (!standing) why = "a dependency before it is this line's own here, and this tree is its line's to install in";
        else if (await DependencyKeys.InputChangedAsync(tree, dependency, ct).ConfigureAwait(false)) why = "this line changed a file that keys it, and this tree is its line's to install in";
        else if (await PlaceAsync(dependency, key, ct).ConfigureAwait(false)) return standing;
        else why = "its folder is gone, and this tree is its line's to install in";
        Left(dependency, why);
        return false;
    }

    /// <summary>A hit placed into the absent folder, and its record written: true where it was placed.</summary>
    private async Task<bool> PlaceAsync(StoreDependency dependency, string key, CancellationToken ct)
    {
        if (dependency.Rule.Bound == true || DependencyStore.Hit(home, key) is null) return false;
        var unworked = TreeFilesRecord.Read(home, tree) is { Unworked: true };
        if (unworked && !TreeFilesRecord.Holding(home, tree, () => { }))
        {
            _stopped = true;
            return false;
        }

        var placed = await Task.Run(() => DependencyStore.Place(home, key, tree, dependency.Folder, ct), ct).ConfigureAwait(false);
        if (placed is null) return false;
        var placement = new TreePlacement(dependency.Path, key, "placed", placed);
        if (unworked)
        {
            if (!TreeFilesRecord.WhileUnworked(home, tree, record => record.With(placement)))
            {
                _stopped = true;
                return true;
            }
        }
        else
        {
            TreeFilesRecord.Change(home, tree, record => record.With(placement));
        }

        _ready.Add(dependency.Folder);
        _said.Add(Noted.Of(StartNoteCodes.StartedDepsPlaced, $"`{dependency.Folder}` was placed from Daoris's store, as `{dependency.Rule.Install}` installed it for the same files.", ("path", dependency.Folder), ("install", dependency.Rule.Install)));
        return true;
    }

    /// <summary>
    /// Daoris's own install of one dependency: recorded <c>installing</c> first, run one at a time per repository, judged against the
    /// status it began from and sealed where the judgement holds; a failed one takes its mark and its half folder away.
    /// </summary>
    private async Task InstallAsync(StoreDependency dependency, string key, CancellationToken ct)
    {
        using var one = await Task.Run(() => RecordLock.Take(RecordLock.PathOf(home, "deps-install-" + Short(repository)), DependencyCommands.InstallBound), ct).ConfigureAwait(false);
        if (await PlaceAsync(dependency, key, ct).ConfigureAwait(false) || _stopped) return;
        if (!TreeFilesRecord.WhileUnworked(home, tree, record => record.With(new TreePlacement(dependency.Path, key, "installed", null) { Installing = true })))
        {
            _stopped = true;
            return;
        }

        var before = await FillJudgement.StatusAsync(tree, ignored: true, ct).ConfigureAwait(false);
        var environment = InstallEnvironment.Of(dependency.Rule);
        var install = dependency.Rule.Install!;
        DependencyCommands.Installing.Value?.Invoke(dependency.Folder);
        var ran = await DependencyCommands.RunAsync(home, tree, install, Path.Combine(tree, dependency.Runs), environment, DependencyCommands.InstallBound, ct).ConfigureAwait(false);
        if (!Still())
        {
            Unrecord(dependency);
            return;
        }

        var after = await FillJudgement.StatusAsync(tree, ignored: true, ct).ConfigureAwait(false);
        var changes = FillJudgement.Changes(before, after, dependency);
        if (ran.Code != 0 || changes.Tracked.Count > 0)
        {
            var tracked = changes.Tracked.ToDictionary(path => path, path => after[path], StringComparer.Ordinal);
            var cleared = TreeFilesRecord.Holding(home, tree, () =>
            {
                if (tracked.Count > 0) PutBack(tracked);
                Clear(dependency);
            });
            if (!cleared)
            {
                Unrecord(dependency);
                _stopped = true;
                return;
            }

            TreeFilesRecord.WhileUnworked(home, tree, record => record.Without(dependency.Path));
            Unplaced(dependency, ran.Code != 0
                ? $"`{install}` failed: {ran.Tail}"
                : $"its install changed {string.Join(", ", changes.Tracked.Select(path => $"`{path}`"))}, which git tracks or does not ignore, so Daoris put it back");
            return;
        }

        var manifest = DependencyManifest.Read(tree, dependency.Folder, dependency.Caches, out var outside, ct);
        var why = dependency.Rule.Bound == true ? StoreWhy.Bound
            : changes.Beside.Count > 0 ? $"its install wrote `{changes.Beside[0]}` outside its folder"
            : ChangedEarlier(dependency) is { } other ? string.Format(System.Globalization.CultureInfo.InvariantCulture, StoreWhy.ChangedOther, other)
            : FillJudgement.Unsealable(tree, dependency.Folder, manifest, outside, (dependency.Rule.Secret ?? []).Select(name => environment.TryGetValue(name, out var value) ? value : ""));
        var sealedHere = why is null && DependencyStore.Seal(home, key, tree, dependency.Folder, manifest, ct);
        var placement = new TreePlacement(dependency.Path, key, sealedHere ? "sealed" : "installed", outside is null ? manifest : null);
        if (!TreeFilesRecord.WhileUnworked(home, tree, record => record.With(placement)))
        {
            Unrecord(dependency);
            _stopped = true;
            return;
        }

        _ready.Add(dependency.Folder);
        if (why is null)
        {
            _said.Add(Noted.Of(StartNoteCodes.StartedDepsInstalled, $"`{dependency.Folder}` was installed by `{install}` before the session began, since no install of it matched these files.", ("path", dependency.Folder), ("install", install)));
        }
        else
        {
            _said.Add(Noted.Of(StartNoteCodes.StartedDepsUnstored, $"`{dependency.Folder}` was installed in this tree and not stored: {why}.", ("path", dependency.Folder), ("why", why)));
        }
    }

    /// <summary>An earlier placement this install changed, read against its record: such an install seals nothing.</summary>
    private string? ChangedEarlier(StoreDependency dependency)
    {
        var record = TreeFilesRecord.Read(home, tree);
        return record?.Placements.Where(each => each.Path != dependency.Path && each.Manifest is not null)
            .FirstOrDefault(each => each.Manifest!.Differs(tree, each.Path.TrimEnd('/'), []) is not null)?.Path.TrimEnd('/');
    }

    /// <summary>The <c>post-checkout</c> hook the making held, where the record holds it owed: run as git would after a checkout, its owing cleared whatever its exit.</summary>
    private async Task<bool> HookAsync(string head, CancellationToken ct)
    {
        if (TreeFilesRecord.Read(home, tree) is not { Unworked: true } record) return false;
        if (!record.HookOwed) return true;
        await WorkingTree.GitAsync(tree, ["hook", "run", "--ignore-missing", "post-checkout", "--", Zeros, head, "1"], ct).ConfigureAwait(false);
        return TreeFilesRecord.WhileUnworked(home, tree, state => state with { HookOwed = false });
    }

    private bool Still() => TreeFilesRecord.Read(home, tree) is { Unworked: true };

    /// <summary>A placement Daoris made, or its cut-off install, removed link-safe under the record's lock with its record.</summary>
    private bool Remove(StoreDependency dependency)
    {
        var removed = TreeFilesRecord.Holding(home, tree, () => Clear(dependency));
        if (removed) removed = TreeFilesRecord.WhileUnworked(home, tree, record => record.Without(dependency.Path));
        if (!removed) _stopped = true;
        return removed;
    }

    private void Clear(StoreDependency dependency)
    {
        var folder = Path.Combine(tree, dependency.Folder);
        if (Path.Exists(folder)) TreeSetAside.DeleteNeverFollowing(folder);
    }

    /// <summary>The stage stopped by a visit: the install's mark taken away, its folder left with no record, the line's own.</summary>
    private void Unrecord(StoreDependency dependency)
    {
        TreeFilesRecord.Change(home, tree, record => record.Without(dependency.Path));
        _stopped = true;
    }

    private TreeFilesOutcome Stop()
    {
        _said.Add(Noted.Of(StartNoteCodes.StartedFilesStopped, "A terminal opened in this tree while Daoris was preparing it, so Daoris stopped there: what it had not finished installing or copying is this line's own, left as it stands."));
        return Outcome() with { Stopped = true };
    }

    private TreeFilesOutcome Outcome() => new(
        [.. _said],
        _stopped,
        _ready.Count == 0 ? null
            : $"Daoris prepared this tree's dependencies before you began, each for the files that key it: {string.Join(", ", _ready.Select(folder => $"`{folder}`"))}. "
              + "They are ready to use: do not run their install where they are (`npm ci` deletes the folder and installs everything again) "
              + "unless a file that keys one changes.");

    private void Kept(StoreDependency dependency)
    {
        _ready.Add(dependency.Folder);
        _said.Add(Noted.Of(StartNoteCodes.StartedDepsKept, $"`{dependency.Folder}` is kept as it was installed: the files that key it, its install and its own files are unchanged.", ("path", dependency.Folder)));
    }

    private void Left(StoreDependency dependency, string why) =>
        _said.Add(Noted.Of(StartNoteCodes.StartedDepsLeft, $"`{dependency.Folder}` is left as this line's own: {why}.", ("path", dependency.Folder), ("why", why)));

    private void Unplaced(StoreDependency dependency, string why) =>
        _said.Add(Noted.Of(StartNoteCodes.StartedDepsUnplaced, $"`{dependency.Folder}` was not placed: {why}.", ("path", dependency.Folder), ("why", why)));

    private static string Short(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
