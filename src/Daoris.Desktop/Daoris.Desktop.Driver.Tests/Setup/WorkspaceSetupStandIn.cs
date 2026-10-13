using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The world a workspace plan reads, in memory (WSSETUP6): the registry with its declarations, each line, the tools, the
/// quests (a publish adds one, open, as the service does), the strikes, the session records and their tool calls, and the
/// plan's lines. So the plan, its tick and its terminal's words are held with no service, git or spawn.
/// </summary>
internal sealed class WorkspaceSetupStandIn : IWorkspaceSetupWorld
{
    private int _asks;

    public List<RegistrationRow> Rows { get; } = [];

    /// <summary>What a repository's line reads as, where it is not the default: an unadopted repository with a README.</summary>
    public Dictionary<string, LineReading> Lines { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SetupTools Tools { get; set; } = new("node", "v22.11.0", null, "daoris", "0.0.1", null);

    public List<QuestView> Quests { get; } = [];

    public Dictionary<string, int> Strikes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<SessionRun> Runs { get; } = [];

    public Dictionary<string, List<ToolTouch>> Touches { get; } = new(StringComparer.Ordinal);

    /// <summary>The plan's lines, as the machine log would be handed them.</summary>
    public List<SetupLine> Logged { get; } = [];

    /// <summary>Each ask published: its workspace, its words and its receiver.</summary>
    public List<(string Workspace, string Sentence, string To)> Published { get; } = [];

    /// <summary>What the ask door answers, where it refuses; null publishes an open quest.</summary>
    public AskAnswer? Refuse { get; set; }

    /// <summary>How often the registry's declarations were read: a tick with no plan to work reads none.</summary>
    public int RegistrationsRead { get; private set; }

    /// <summary>Told each quest a publish made, as the service's own quest list would be: how a look's ledger hears of it.</summary>
    public Action<QuestView>? Publishes { get; set; }

    /// <summary>What reading the registry throws, as a service that went away does; null answers.</summary>
    public Exception? Fails { get; set; }

    /// <summary>Each repository whose line was read, in order: a press reads one, a skip is never read again.</summary>
    public List<string> LinesRead { get; } = [];

    /// <summary>A repository with a checkout here, in <paramref name="workspace"/>, declaring nothing.</summary>
    public static RegistrationRow Row(string repository, string workspace = "work", string? root = null, bool adopted = false, string? summary = null) =>
        new(repository, workspace, root ?? $"/checkouts/{repository}", adopted, summary, [], [], [], [], false, false, []);

    /// <summary>Add a repository to the registry, and return this.</summary>
    public WorkspaceSetupStandIn With(params RegistrationRow[] rows)
    {
        Rows.AddRange(rows);
        return this;
    }

    /// <summary>A quest as the service holds it.</summary>
    public QuestView Quest(string id, string to, string title, string status = "Open", string from = "ask #x")
    {
        var quest = new QuestView(id, from, to, title, "Stand-in work.", status) { Deletable = status == "Open" };
        Quests.Add(quest);
        return quest;
    }

    /// <summary>Close a quest as its session would, done or declined, with what it said.</summary>
    public void Close(string id, string status, string? note = null)
    {
        var at = Quests.FindIndex(quest => quest.Id == id);
        Quests[at] = Quests[at] with { Status = status, Note = note, Deletable = false };
    }

    /// <summary>The service's delete (D95): the quest is gone.</summary>
    public void Delete(string id) => Quests.RemoveAll(quest => quest.Id == id);

    /// <summary>Its line merged and followed: the row adopted and declaring, which the service calls registered.</summary>
    public void SetUp(string repository)
    {
        var at = Rows.FindIndex(row => row.Repository == repository);
        Rows[at] = Rows[at] with { Adopted = true, Summary = $"What {repository} owns.", Registered = true };
    }

    /// <summary>The set-up quest the plan's press asked of <paramref name="repository"/>, the newest.</summary>
    public QuestView SetupOf(string repository) =>
        Quests.Last(quest => quest.To == repository && SetupQuests.IsSetup(quest.Title));

    public Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct)
    {
        RegistrationsRead++;
        return Fails is { } failure ? Task.FromException<IReadOnlyList<RegistrationRow>>(failure) : Task.FromResult<IReadOnlyList<RegistrationRow>>([.. Rows]);
    }

    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RepoView>>([.. Rows.Select(row => new RepoView(row.Repository, row.Adopted, row.Root, row.Workspace))]);

    public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct)
    {
        LinesRead.Add(repository.Repository);
        return Task.FromResult(Lines.TryGetValue(repository.Repository, out var line)
            ? line
            : new LineReading(new LayoutReaderTests.Scratch().File("README.md", $"# {repository.Repository}\n").Read(), null));
    }

    public Task<SetupTools> ToolsAsync(CancellationToken ct) => Task.FromResult(Tools);

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests]);

    public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

    public RepositoryDescription? Describe(string root) => null;

    public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, CancellationToken ct)
    {
        Published.Add((workspace, sentence, to));
        if (Refuse is { } refused) return Task.FromResult(refused);

        // Numbered by the asks, never by what the list holds now: a quest deleted would hand its id to the next.
        var ask = $"a{++_asks}";
        var id = $"q{_asks}";
        var lines = sentence.Split('\n');
        var quest = new QuestView(id, $"ask #{ask}", to, lines[0], string.Join('\n', lines.Skip(2)), "Open") { Deletable = true };
        Quests.Add(quest);
        Publishes?.Invoke(quest);
        return Task.FromResult(new AskAnswer(true, $"Asked as `#{ask}` in `{workspace}`.", ask, id));
    }

    public Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(Strikes, StringComparer.OrdinalIgnoreCase));

    public Task<IReadOnlyList<SessionRun>> RunsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRun>>([.. Runs]);

    public IReadOnlyList<ToolTouch> Touched(string session) => Touches.TryGetValue(session, out var touched) ? touched : [];

    public void Said(SetupLine line) => Logged.Add(line);
}
