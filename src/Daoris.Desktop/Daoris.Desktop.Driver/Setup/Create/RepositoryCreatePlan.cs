namespace Daoris.Driver;

/// <summary>
/// A creation's plan (<c>daoris repository create … --plan</c>): the ask resolved on this machine and every refusal at once, written from the
/// registry, the identities of the workspace's repositories, the folder as it stands and an unfinished receipt of the same name; it writes
/// nothing. A plan whose folder holds exactly the empty commit an unfinished receipt names is that receipt's resumption, with its digest.
/// </summary>
public static class RepositoryCreatePlans
{
    /// <summary>The line a history starts on where the workspace's repositories name none.</summary>
    public const string DefaultLine = "main";

    public static async Task<CreatePlanned> PlanAsync(CreateDoors doors, CreateAsk asked, CancellationToken ct = default)
    {
        var ask = asked with
        {
            Name = asked.Name.Trim(),
            Purpose = asked.Purpose.Trim(),
            Kind = asked.Kind.Trim().ToLowerInvariant(),
            Workspace = RemoteTarget.Workspace(asked.Workspace),
            Line = string.IsNullOrWhiteSpace(asked.Line) ? null : asked.Line.Trim(),
            Under = string.IsNullOrWhiteSpace(asked.Under) ? null : asked.Under.Trim(),
            Work = string.IsNullOrWhiteSpace(asked.Work) ? null : asked.Work.Trim(),
        };
        var workspace = ask.Workspace!;
        var world = doors.World;
        var refusals = new List<CreateRefusal>(CreateJudge.Asked(ask));
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        var config = doors.Config();
        var growth = GrowthRules.Resolve(config, null, workspace).Value;
        if (growth == GrowthKey.Off) refusals.Add(new(CreateRefusals.GrowthOff, GrowthRules.OffOpening(workspace)));

        var resume = CreateJudge.NameUsable(ask.Name) ? new CreateReceipts(doors.Home).Unfinished(ask.Name) : null;
        if (resume is not null && !(string.Equals(resume.Workspace, workspace, StringComparison.OrdinalIgnoreCase) && resume.Ask.Kind == ask.Kind))
        {
            resume = null;
        }

        if (CreateJudge.Registered(ask.Name, registry) is { } taken
            && !(resume is not null && registry.FirstOrDefault(row => string.Equals(row.Repository, ask.Name, StringComparison.OrdinalIgnoreCase)) is { } mine
                 && Same(mine.Root, resume.Folder)))
        {
            refusals.Add(taken);
        }

        if (!CreateJudge.WorkspaceExists(workspace, registry))
        {
            refusals.Add(new(CreateRefusals.NoWorkspace, $"workspace `{workspace}` does not exist: no repository is registered in it. Name one that does, or leave `--workspace` out for the default."));
        }

        if (!world.LocalService)
        {
            refusals.Add(new(CreateRefusals.ServiceNotLocal,
                "the service is not on this machine, and a folder made here means nothing there: open a department through this machine's own service."));
        }

        var inWorkspace = registry.Where(row => string.Equals(RemoteTarget.Workspace(row.Workspace), workspace, StringComparison.OrdinalIgnoreCase)).ToList();
        var folder = resume?.Folder ?? Folder(ask, inWorkspace, world, refusals);
        if (folder.Length > 0)
        {
            await FolderAsync(ask, folder, resume, refusals, ct).ConfigureAwait(false);
        }

        string? line = null;
        CreateAuthor? author = null;
        if (ask.Kind == PlaceKind.Git)
        {
            if (!await CreateHistory.AvailableAsync(ct).ConfigureAwait(false))
            {
                refusals.Add(new(CreateRefusals.NoGit, "git did not answer on this machine, and a repository's history is made with it: install git, or open a folder department with `--kind folder`."));
            }

            var gitRows = inWorkspace.Where(row => row.Kind == PlaceKind.Git && row.Root is { Length: > 0 }
                && !string.Equals(row.Repository, ask.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            line = resume is not null ? resume.Line : ask.Line ?? await LineAsync(gitRows, world, config, ct).ConfigureAwait(false);
            author = resume is not null ? resume.Author : await AuthorAsync(gitRows, world, ct).ConfigureAwait(false);
            if (author is null)
            {
                refusals.Add(new(CreateRefusals.NoIdentity,
                    $"no git identity is chosen for the repositories of `{workspace}`, so its first commit has no author: choose one with "
                    + $"`daoris git use --workspace {workspace} --host <host> <identity>`, then press again."));
            }
        }

        var remote = resume is not null ? resume.RemoteStep : world.RemoteStepOf(config, ask.Name, workspace);
        var steps = StepsOf(ask, workspace, folder, line, author, remote);
        return new CreatePlanned(ask, workspace, folder, line, author, steps, refusals) { RemoteStep = remote, Resumes = resume?.Digest, Growth = growth };
    }

    /// <summary>The folder the department's folder is made at: under the asked parent, else the workspace's roots' parent; empty where none can be said.</summary>
    private static string Folder(CreateAsk ask, IReadOnlyList<RepoView> inWorkspace, ICreateWorld world, List<CreateRefusal> refusals)
    {
        var parent = ask.Under;
        if (parent is not null && !Path.IsPathRooted(parent))
        {
            refusals.Add(new(CreateRefusals.UnderMissing, $"`--under {parent}` is not an absolute folder: name where on this machine the folder goes."));
            return "";
        }

        parent ??= CreateJudge.Parent(inWorkspace, world.CreatedAt);
        if (parent is null)
        {
            refusals.Add(new(CreateRefusals.NoFolder,
                $"no repository of `{ask.Workspace}` has a checkout on this machine to say where its repositories sit: name the folder to make it in with `--under <folder>`."));
            return "";
        }

        if (!Directory.Exists(parent))
        {
            refusals.Add(new(CreateRefusals.UnderMissing, $"{parent} is not a folder on this machine, so the department's folder cannot be made in it."));
            return "";
        }

        return Path.Combine(parent, ask.Name);
    }

    /// <summary>Whether the folder is the press's to take: none there, an empty one, or the folder an unfinished receipt of this department made.</summary>
    private static async Task FolderAsync(CreateAsk ask, string folder, CreateReceipt? resume, List<CreateRefusal> refusals, CancellationToken ct)
    {
        var read = await CreateHistory.ReadAsync(folder, ct).ConfigureAwait(false);
        var taken = read.State switch
        {
            CreateFolderState.Missing or CreateFolderState.Empty => true,
            CreateFolderState.Partial or CreateFolderState.Fresh => resume is not null && ask.Kind == PlaceKind.Git
                && (read.State == CreateFolderState.Partial || resume.Commit is null || resume.Commit == read.Commit),
            _ => false,
        };
        if (!taken)
        {
            refusals.Add(new(CreateRefusals.FolderExists, resume is null
                ? $"{folder} exists and is not empty, so nothing is made there: name another department, or `--under` another folder."
                : $"{folder} holds more than the empty commit the receipt of `{ask.Name}` names, so it is not the folder that receipt made: nothing was changed. Remove it yourself, or name another department."));
        }
    }

    private static async Task<string> LineAsync(IReadOnlyList<RepoView> rows, ICreateWorld world, DriverConfig config, CancellationToken ct)
    {
        var lines = new List<string>();
        foreach (var row in rows)
        {
            if (await world.LineOfAsync(row, config, ct).ConfigureAwait(false) is { Length: > 0 } line) lines.Add(line);
        }

        return CreateJudge.Most(lines) ?? DefaultLine;
    }

    private static async Task<CreateAuthor?> AuthorAsync(IReadOnlyList<RepoView> rows, ICreateWorld world, CancellationToken ct)
    {
        var authors = new List<CreateAuthor>();
        foreach (var row in rows)
        {
            if (await world.AuthorOfAsync(row, ct).ConfigureAwait(false) is { } author) authors.Add(author);
        }

        return CreateJudge.Most(authors);
    }

    private static IReadOnlyList<CreateStep> StepsOf(CreateAsk ask, string workspace, string folder, string? line, CreateAuthor? author, string? remote)
    {
        var git = ask.Kind == PlaceKind.Git;
        var steps = new List<CreateStep> { new(CreateOperations.Folder, $"make the folder {(folder.Length > 0 ? folder : "(none yet)")}") };
        if (git)
        {
            steps.Add(new(CreateOperations.History,
                $"`git init` on `{line ?? RepositoryCreatePlans.DefaultLine}`, one empty commit as {(author is null ? "(no identity)" : $"{author.Name} <{author.Email}>")}, no remote, no push"));
        }

        steps.Add(new(CreateOperations.Registration, $"register `{ask.Name}` in `{workspace}` as {PlaceKind.Said(ask.Kind)}, not adopted"));
        steps.Add(new(CreateOperations.Driving, remote is null
            ? $"drive on, hold off, inheriting `{workspace}`'s workflow and landing"
            : $"drive stays off: the workflow it inherits has a `{remote}` step and `{ask.Name}` has no remote"));
        if (git) steps.Add(new(CreateOperations.SetUp, "ask its own session to set it up (the set-up quest)"));
        return steps;
    }

    private static bool Same(string? left, string right) =>
        left is { Length: > 0 } && string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);
}
