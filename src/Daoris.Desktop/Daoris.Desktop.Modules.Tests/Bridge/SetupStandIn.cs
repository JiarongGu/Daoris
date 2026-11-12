using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The world a set-up press reads, standing in: the real one asks git for the line and starts <c>node</c> and
/// <c>daoris</c> for their versions, which the fast half never does. `atlas` is registered, not adopted, with a checkout
/// here; its line holds nothing of Daoris's, and the tools a child finds answer their versions.
/// </summary>
internal sealed class SetupStandIn : ISetupWorld
{
    public List<RepoView> Registry { get; } = [new("atlas", false, "/fixture/atlas", "work")];

    public LayoutFacts Facts { get; set; } = new() { Line = "main", Commit = "0123456789abcdef0123456789abcdef01234567" };

    public SetupTools Tools { get; init; } = new("node", "v24.1.0", null, "daoris", "0.9.0", null);

    public AskAnswer Answer { get; init; } = new(true, "Asked as `#a1b2c3d4` in `work`.", "a1b2c3d4", "q1a2b3c4");

    /// <summary>Each ask the press published: its workspace, its words and its receiver.</summary>
    public List<(string Workspace, string Sentence, string To)> Published { get; } = [];

    /// <summary>Its sessions work apart, as a landing preset's workflow sets them.</summary>
    public IsolateStep? IsolationOf(DriverConfig config, RepoView repository) => IsolateStep.Trees;

    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RepoView>>(Registry);

    public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) =>
        Task.FromResult(new LineReading(Facts, null));

    public Task<SetupTools> ToolsAsync(CancellationToken ct) => Task.FromResult(Tools);

    /// <summary>What the doctrine tool says of a checkout: by default, that nothing has moved.</summary>
    public DoctrineReading Doctrine { get; set; } = new(new DoctrineStatus { Synced = true }, null);

    /// <summary>The read itself, where a test runs the doctrine tool over a fixture; null answers <see cref="Doctrine"/>.</summary>
    public Func<RepoView, CancellationToken, Task<DoctrineReading>>? Reads { get; init; }

    public Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct) =>
        Reads?.Invoke(repository, ct) ?? Task.FromResult(Doctrine);

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([]);

    public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

    public RepositoryDescription? Describe(string root) => null;

    public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, SetupThen? then, CancellationToken ct)
    {
        Published.Add((workspace, sentence, to));
        return Task.FromResult(Answer);
    }

    /// <summary>
    /// The local host's doors a module coming up reads, every list empty: what the help route's facts read beside the press's
    /// world, which stands in for the rest.
    /// </summary>
    public sealed class EmptyHost : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
