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
/// each group and each dependency in rule order is kept where its key, record and companions hold, placed from the store at a hit, or
/// installed by Daoris and sealed where its judgement holds; the <c>post-checkout</c> hook the making held runs; each kept placement's
/// other writes are read, one gone installing it again; and the machine-local files are copied. In a worked tree it installs, removes
/// and runs nothing but a key's <c>toolchain</c> reads: a dependency is kept where its record holds, or placed from a hit into an absent
/// folder where every one before it stands kept or placed here and no group names it; every other is the line's own, said.
/// </summary>
/// <remarks>
/// The mark is read again under the record's lock before each write-back, removal, placement record, hook and copy, and the lock is held
/// through each write, never while an install or the hook runs: once a terminal's visit has taken it, which ends a running install or
/// hook, the stage stops, takes its <c>installing</c> mark away and leaves what it had not finished as the line's own.
/// </remarks>
internal sealed class TreeFilesStage(string home, string root, string tree, string repository)
{
    private const string Zeros = "0000000000000000000000000000000000000000";

    private readonly List<Noted> _said = [];
    private readonly List<string> _ready = [];
    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _grouped = new(StringComparer.Ordinal);
    private readonly HashSet<string> _installOnly = new(StringComparer.Ordinal);
    private IReadOnlyList<StoreDependency> _order = [];
    private FileRules? _rules;
    private string? _own;
    private CancellationToken _visited;
    private bool _stopped;

    internal async Task<TreeFilesOutcome> RunAsync(CancellationToken ct)
    {
        var head = await WorkingTree.HeadAsync(tree, ct).ConfigureAwait(false);
        if (head is null) return TreeFilesOutcome.None;
        using var watch = TreeVisit.Watch(tree);
        _visited = watch.Token;
        var unworked = TreeFilesRecord.Read(home, tree) is { Unworked: true };
        var deps = await StoreDependencyRules.ReadAsync(home, tree, repository, head, ct).ConfigureAwait(false);
        (_order, _rules) = (deps.Each, deps.Rules);
        if (deps.Rules.Unread is { } unread) _said.Add(Noted.Of(StartNoteCodes.StartedFilesUnread, $"this machine's file rules for `{repository}` do not read ({unread}), so Daoris held every install and copy they could restrict here.", ("repository", repository), ("why", unread)));

        if (unworked && !await WriteBackAsync(ct).ConfigureAwait(false)) return Stop();
        var before = new List<string>();
        foreach (var dependency in deps.Each)
        {
            var key = await DependencyKeys.KeyAsync(home, tree, head, repository, dependency, before, ct).ConfigureAwait(false);
            before.Add(key);
            _keys[dependency.Path] = key;
        }

        if (!Groups(unworked)) return Stop();
        var waiting = 0;
        var standing = true;
        foreach (var dependency in deps.Each)
        {
            var key = _keys[dependency.Path];
            if (_grouped.Contains(dependency.Path))
            {
                Kept(dependency);
            }
            else if (unworked)
            {
                var held = await UnworkedAsync(dependency, key, ct).ConfigureAwait(false);
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
            if (!await OthersAsync(ct).ConfigureAwait(false)) return Stop();
            if (!await RepositorySetup.RunAsync(home, repository, tree, head, deps.Each, _said, ct, _visited).ConfigureAwait(false)) return Stop();
            if (!TreeFilesRecord.Holding(home, tree, () => { })) return Stop();
        }

        var shared = await TreeSharing.RunAsync(home, root, tree, repository, deps.Rules, deps.Floor, ct).ConfigureAwait(false);
        _said.AddRange(shared.Said);
        var based = TreeBaseWords.Say(home, tree, deps.Rules, deps.Floor);
        _said.AddRange(based.Said);
        var copied = await TreeLocalInputs.CopyAsync(root, tree, ct).ConfigureAwait(false);
        if (copied.Copied.Count > 0) RecordCopies(copied.Copied);
        if (copied.Line() is { } line) _said.Add(line);
        foreach (var each in copied.Declined) _said.Add(each.Line());
        var outcome = Outcome();
        var told = string.Join(' ', new[] { outcome.Told, shared.Told, based.Told }.OfType<string>());
        return told.Length == 0 ? outcome : outcome with { Told = told };
    }

    private void RecordCopies(IReadOnlyList<string> copied) => TreeFilesRecord.Change(home, tree, state => state with
    {
        Copies = state.Copies.Concat(copied.Select(path => KeyValuePair.Create(path, TreeCopies.HashOf(Path.Combine(tree, path)))).Where(each => each.Value is not null).Select(each => KeyValuePair.Create(each.Key, each.Value!)))
            .GroupBy(each => each.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal),
    });

    /// <summary>Every tracked change and every path git does not ignore, which only a cut-off start's own install or hook can have left, put back.</summary>
    private async Task<bool> WriteBackAsync(CancellationToken ct)
    {
        var changed = await FillJudgement.StatusAsync(tree, ignored: false, ct).ConfigureAwait(false);
        if (changed.Count == 0) return true;
        var wrote = TreeFilesRecord.Holding(home, tree, () => PutBack(changed));
        if (!wrote) return false;
        TreeFilesRecord.WhileUnworked(home, tree, record => record with { HookOwed = true });
        _said.Add(Noted.Of(StartNoteCodes.StartedFilesWrittenBack, $"Daoris put back what a start cut off before it left changed here: {string.Join(", ", changed.Keys.Select(path => $"`{path}`"))}.", ("written", (IReadOnlyList<string>)[.. changed.Keys])));
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

    /// <summary>
    /// Each group the record holds, judged once before any member: kept whole; else, in an unworked tree, each member removed to be
    /// installed again in rule order, never placed by its own key. In a worked tree a failing group's members are the line's own.
    /// </summary>
    private bool Groups(bool unworked)
    {
        if (TreeFilesRecord.Read(home, tree) is not { } record) return true;
        foreach (var group in record.Groups)
        {
            if (DependencyGroups.Fails(tree, record, group, _order, _keys, others: false) is null)
            {
                _grouped.UnionWith(group.Members);
                continue;
            }

            if (!unworked) continue;
            foreach (var member in group.Members)
            {
                if (KeptByAnotherRule(member))
                {
                    if (!TreeFilesRecord.WhileUnworked(home, tree, state => state.Without(member))) return false;
                    continue;
                }

                _installOnly.Add(member);
                if (!Remove(member)) return false;
            }

            if (!TreeFilesRecord.WhileUnworked(home, tree, state => state.Without(group))) return false;
        }

        return true;
    }

    private bool KeptByAnotherRule(string path) =>
        _order.All(each => each.Path != path) && _rules!.Rules.LastOrDefault(each => each.Rule.Matches(path)) is { Rule.Kind: FileKind.Intermediate or FileKind.Cache };

    /// <summary>One dependency in an unworked tree; true where a restriction holds its install for an allow.</summary>
    private async Task<bool> UnworkedAsync(StoreDependency dependency, string key, CancellationToken ct)
    {
        var record = TreeFilesRecord.Read(home, tree);
        var placement = record?.Of(dependency.Path);
        var exists = Path.Exists(Path.Combine(tree, dependency.Folder));
        if (placement is { Installing: true })
        {
            if (!Remove(dependency.Path)) return false;
            (placement, exists) = (null, false);
        }

        if (placement is not null && (exists || placement.Manifest is { Absent: true }))
        {
            if (placement.Manifest?.Differs(tree, dependency.Folder, dependency.Caches) is not null)
            {
                Left(dependency, DepsWhy.ChangedIt());
                return false;
            }

            if (placement.Key == key && InstallWrites.CompanionsDiffer(tree, placement) is null)
            {
                Kept(dependency);
                return false;
            }

            if (!Remove(dependency.Path)) return false;
        }
        else if (exists)
        {
            Left(dependency, DepsWhy.NotPlaced());
            return false;
        }

        if (Placeable(dependency) && await PlaceAsync(dependency, key, ct).ConfigureAwait(false)) return false;
        if (_stopped) return false;
        return await InstallUnlessHeldAsync(dependency, key, ct).ConfigureAwait(false);
    }

    /// <summary>The install, unless the person's permissions deny it or hold it for an allow: true where one holds it.</summary>
    private async Task<bool> InstallUnlessHeldAsync(StoreDependency dependency, string key, CancellationToken ct)
    {
        var install = dependency.Rule.Install!;
        var act = FileActs.Run(install);
        switch (_rules!.Judge(act))
        {
            case FileVerdict.Denied:
                FileAsks.Close(home, repository, FileAsk.Run, act);
                Unplaced(dependency, DepsWhy.DeniedBy(install));
                return false;
            case FileVerdict.Held:
                FileAsks.Note(home, new FileAsk(repository, Moments.Write(DateTimeOffset.UtcNow), FileAsk.Run, act) { Person = _rules.NamesPerson(act) });
                Unplaced(dependency, DepsWhy.HeldFor(install));
                return true;
        }

        FileAsks.Close(home, repository, FileAsk.Run, act);
        await InstallAsync(dependency, key, ct).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// One dependency in a worked tree: kept where its key, record, companions and other writes hold, or placed from a hit into an
    /// absent folder where no tracked input of its key has a working change, no group names it and every dependency before it stands kept
    /// or placed here. Whether it stands as its key says.
    /// </summary>
    private async Task<bool> WorkedAsync(StoreDependency dependency, string key, bool standing, CancellationToken ct)
    {
        var record = TreeFilesRecord.Read(home, tree);
        var placement = record?.Of(dependency.Path);
        var exists = Path.Exists(Path.Combine(tree, dependency.Folder));
        DepsReason why;
        if (record?.GroupOf(dependency.Path) is { } group)
        {
            why = DepsWhy.GroupedWith(DependencyGroups.Others(group, dependency.Path));
        }
        else if (placement is { Installing: false } && (exists || placement.Manifest is { Absent: true }) && placement.Key == key
            && placement.Manifest?.Differs(tree, dependency.Folder, dependency.Caches) is null
            && InstallWrites.CompanionsDiffer(tree, placement) is null && InstallWrites.OtherGone(tree, placement) is null)
        {
            Kept(dependency);
            return standing;
        }
        else if (exists)
        {
            why = placement is null ? DepsWhy.NotPlaced() : placement.Key != key ? DepsWhy.KeyMovedSince() : DepsWhy.ChangedIt();
        }
        else if (!standing)
        {
            why = DepsWhy.BeforeIsOwn(_own!);
        }
        else if (await DependencyKeys.InputChangedAsync(tree, dependency, ct).ConfigureAwait(false))
        {
            why = DepsWhy.InputChangedHere();
        }
        else if (await PlaceAsync(dependency, key, ct).ConfigureAwait(false))
        {
            return standing;
        }
        else
        {
            why = DepsWhy.FolderGone();
        }

        Left(dependency, why);
        _own ??= dependency.Folder;
        return false;
    }

    /// <summary>Whether the store may place it by its own key: never a group's member, nor one this stage must install so its producer runs.</summary>
    private bool Placeable(StoreDependency dependency) =>
        !_installOnly.Contains(dependency.Path) && TreeFilesRecord.Read(home, tree)?.GroupOf(dependency.Path) is null;

    /// <summary>A hit placed into the absent folder, and its record written: true where it is placed.</summary>
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
    /// Daoris's own install of one dependency, under its admission: recorded <c>installing</c> first, judged against the status it began
    /// from, its writes beside its folder sorted into companions and other writes, grouped with each earlier placement it changed, and
    /// sealed where the judgement holds; a failed one takes its mark and its half folder away.
    /// </summary>
    private async Task InstallAsync(StoreDependency dependency, string key, CancellationToken ct)
    {
        using var admitted = await InstallAdmission.TakeAsync(home, repository, tree, dependency, key, ct).ConfigureAwait(false);
        if (Placeable(dependency) && await PlaceAsync(dependency, key, ct).ConfigureAwait(false)) return;
        if (_stopped) return;
        if (!TreeFilesRecord.WhileUnworked(home, tree, record => record.With(new TreePlacement(dependency.Path, key, "installed", null) { Installing = true })))
        {
            _stopped = true;
            return;
        }

        var before = await FillJudgement.StatusAsync(tree, ignored: true, ct).ConfigureAwait(false);
        var common = await GitCommonFolder.ListAsync(tree, ct).ConfigureAwait(false);
        var environment = InstallEnvironment.Of(dependency.Rule);
        var install = dependency.Rule.Install!;
        DependencyCommands.Installing.Value?.Invoke(dependency.Folder);
        var ran = await DependencyCommands.RunAsync(home, tree, install, Path.Combine(tree, dependency.Runs), environment, DependencyCommands.InstallBound, ct, _visited).ConfigureAwait(false);
        var wrote = GitCommonFolder.Changed(common, await GitCommonFolder.ListAsync(tree, ct).ConfigureAwait(false));
        InstallAdmission.Wrote(home, repository, dependency.Path, wrote.Count > 0);
        if (wrote.Count > 0) _said.Add(Noted.Of(StartNoteCodes.StartedDepsCommon, $"`{dependency.Folder}`'s install changed git's own files for this repository: {string.Join(", ", wrote.Select(name => $"`{name}`"))}.", ("path", dependency.Folder), ("changed", wrote)));
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
                Clear(dependency.Path);
            });
            if (!cleared)
            {
                Unrecord(dependency);
                return;
            }

            TreeFilesRecord.WhileUnworked(home, tree, record => record.Without(dependency.Path));
            Unplaced(dependency, ran.Code != 0 ? DepsWhy.FailedSaying(install, ran.Tail) : DepsWhy.TrackedPutBack(changes.Tracked));
            return;
        }

        var manifest = DependencyManifest.Read(tree, dependency.Folder, dependency.Caches, out var outside, ct);
        var (companions, others) = InstallWrites.Of(tree, changes.Beside, _rules!);
        var current = TreeFilesRecord.Read(home, tree)!;
        var changed = DependencyGroups.Changed(tree, current, dependency.Path, _order);
        // A fill seals only in a tree that held, as it began, nothing ignored but the folders of the dependencies Daoris placed before it.
        var held = before.Where(each => each.Value == "!!" && each.Key != dependency.Path && current.Of(each.Key) is null).Select(each => each.Key).FirstOrDefault();
        var why = dependency.Rule.Bound == true ? DepsWhy.BoundToTree()
            : changes.Beside.Count > 0 ? DepsWhy.WroteBeside(changes.Beside[0])
            : held is not null ? DepsWhy.HeldIgnored(held)
            : changed.Count > 0 ? DepsWhy.GroupFormed(changed[0].TrimEnd('/'))
            : FillJudgement.Unsealable(tree, dependency.Folder, manifest, outside, (dependency.Rule.Secret ?? []).Select(name => environment.TryGetValue(name, out var value) ? value : ""));
        var sealedHere = why is null && DependencyStore.Seal(home, key, tree, dependency.Folder, manifest, ct);
        if (why is null && !sealedHere) why = DepsWhy.SealFailed();
        var placement = new TreePlacement(dependency.Path, key, sealedHere ? "sealed" : "installed", outside is null ? manifest : null) { Companions = companions, Others = others };
        var recorded = TreeFilesRecord.WhileUnworked(home, tree, record =>
        {
            var state = record.With(placement);
            return changed.Count == 0 ? state : DependencyGroups.Formed(tree, state, DependencyGroups.Members(state, dependency.Path, changed, _order), _keys, _order);
        });
        if (!recorded)
        {
            Unrecord(dependency);
            return;
        }

        _ready.Add(dependency.Folder);
        if (why is null)
        {
            _said.Add(Noted.Of(StartNoteCodes.StartedDepsInstalled, $"`{dependency.Folder}` was installed by `{install}` before the session began, since no install of it matched these files.", ("path", dependency.Folder), ("install", install)));
        }
        else
        {
            _said.Add(Noted.Of(StartNoteCodes.StartedDepsUnstored, $"`{dependency.Folder}` was installed in this tree and not stored: {why.Text}.", why.With(("path", dependency.Folder))));
        }
    }

    /// <summary>The <c>post-checkout</c> hook the making held, where the record holds it owed: run as git would after a checkout, its owing cleared whatever its exit.</summary>
    private async Task<bool> HookAsync(string head, CancellationToken ct)
    {
        if (TreeFilesRecord.Read(home, tree) is not { Unworked: true } record) return false;
        if (!record.HookOwed) return true;
        using var visitable = CancellationTokenSource.CreateLinkedTokenSource(ct, _visited);
        try
        {
            await WorkingTree.GitAsync(tree, ["hook", "run", "--ignore-missing", "post-checkout", "--", record.HookFrom ?? Zeros, head, "1"], visitable.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }

        return TreeFilesRecord.WhileUnworked(home, tree, state => state with { HookOwed = false });
    }

    /// <summary>
    /// After the hook, at every unworked stage: each kept placement's and group's other writes read; one gone, its own rule having cleared
    /// or moved it, fails it, and it is removed and installed again in rule order, never placed, so its producer runs before the hand.
    /// </summary>
    private async Task<bool> OthersAsync(CancellationToken ct)
    {
        if (TreeFilesRecord.Read(home, tree) is not { Unworked: true } record) return false;
        var again = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in _order)
        {
            if (again.Contains(dependency.Path) || record.Of(dependency.Path) is not { Installing: false }) continue;
            var group = record.GroupOf(dependency.Path);
            IReadOnlyList<string> members = group?.Members ?? [dependency.Path];
            if (members.All(member => record.Of(member) is not { } each || InstallWrites.OtherGone(tree, each) is null)) continue;
            foreach (var member in members)
            {
                again.Add(member);
                _installOnly.Add(member);
                if (!Remove(member)) return false;
            }

            if (group is not null && !TreeFilesRecord.WhileUnworked(home, tree, state => state.Without(group))) return false;
        }

        foreach (var dependency in _order.Where(each => again.Contains(each.Path)))
        {
            _ready.Remove(dependency.Folder);
            _said.RemoveAll(line => line.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsKept.Code && part.Values.Any(value => value.Key == "path" && Equals(value.Value, dependency.Folder))));
            await InstallUnlessHeldAsync(dependency, _keys[dependency.Path], ct).ConfigureAwait(false);
            if (_stopped) return false;
        }

        return true;
    }

    private bool Still() => TreeFilesRecord.Read(home, tree) is { Unworked: true };

    /// <summary>A placement Daoris made, or its cut-off install, removed link-safe under the record's lock with its companions and its record.</summary>
    private bool Remove(string path)
    {
        var removed = TreeFilesRecord.Holding(home, tree, () =>
        {
            Clear(path);
            foreach (var companion in TreeFilesRecord.Read(home, tree)?.Of(path)?.Companions ?? []) Clear(companion.Path);
        });
        if (removed) removed = TreeFilesRecord.WhileUnworked(home, tree, record => record.Without(path));
        if (!removed) _stopped = true;
        return removed;
    }

    private void Clear(string path)
    {
        var folder = Path.Combine(tree, path.TrimEnd('/'));
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

    private void Left(StoreDependency dependency, DepsReason why) =>
        _said.Add(Noted.Of(StartNoteCodes.StartedDepsLeft, $"`{dependency.Folder}` is left as this line's own: {why.Text}.", why.With(("path", dependency.Folder))));

    private void Unplaced(StoreDependency dependency, DepsReason why) =>
        _said.Add(Noted.Of(StartNoteCodes.StartedDepsUnplaced, $"`{dependency.Folder}` was not placed: {why.Text}.", why.With(("path", dependency.Folder))));
}
