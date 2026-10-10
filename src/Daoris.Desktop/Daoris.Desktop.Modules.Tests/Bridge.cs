using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The bridge, from the host's side — what the platform page is actually talking to.
/// </summary>
/// <remarks>
/// <para>These modules are the whole contract between the page and this machine, and until now it was
/// asserted on NEITHER side: the page's own suite mocks the bridge, and this half had no test project
/// at all. A mock agreeing with a mock proves the two mocks agree.</para>
///
/// <para><b>Hermetic by construction.</b> Every module here resolves a path under the Daoris home
/// — `driver.json`, `remotes.json`, the harness profile tree — so a test that did not redirect them
/// would read, and WRITE, the developer's real machine. The environment is set
/// before anything is constructed, because <see cref="Daoris.Driver.HarnessRoster"/> and
/// <see cref="Daoris.Driver.DriverConfig"/> both capture their path at construction.</para>
/// </remarks>
public abstract class Bridge : IDisposable
{
    private readonly Dictionary<string, string?> _restore = new();

    protected Bridge(string? repositoryFixture = null)
    {
        Home = Path.Combine(repositoryFixture is null ? Path.Combine(Path.GetTempPath(), "daoris-modules-tests")
            : Path.Combine(RepositoryRoot(), "_fixtures", repositoryFixture), Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Home);

        // Set BEFORE any module or loop exists — see the remarks above. The home first (D63): every
        // other default derives from it, so a file added under the home later is scratch already.
        Redirect("DAORIS_HOME", Home);
        Redirect("DAORIS_DRIVER_CONFIG", Path.Combine(Home, "driver.json"));
        Redirect("DAORIS_REMOTE_CONFIG", Path.Combine(Home, "remotes.json"));
        Redirect("DAORIS_HARNESS_CONFIG", Path.Combine(Home, "harnesses.json"));
        // The environment pair outranks the remotes FILE for the whole machine (D48 §5). A developer
        // with it set would otherwise change what these tests observe.
        Redirect("DAORIS_REMOTE_URL", null);
        Redirect("DAORIS_REMOTE_KEY", null);
        Redirect("DAORIS_REMOTE_WORKSPACE", null);

        // Shenora's REAL bus, subscribed to — not a stub of it. A fake implementing this interface
        // would be a second guess at the shape the page receives, and a mock agreeing with a mock
        // proves only that the two mocks agree.
        Bus = new EventBus(NullLogger<EventBus>.Instance);
        Bus.SubscribeToAll(message =>
        {
            lock (_raised) _raised.Add(message);
            return Task.CompletedTask;
        });
    }

    private readonly List<EventMessage> _raised = [];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository fixtures require a checkout.");
    }

    /// <summary>What the module raised. A page listens to these, so they are part of the contract.</summary>
    protected IReadOnlyList<EventMessage> Raised
    {
        get { lock (_raised) return [.. _raised]; }
    }

    /// <summary>The bus the modules are constructed with.</summary>
    protected EventBus Bus { get; }

    /// <summary>This test's own `.daoris` — every file the modules touch lives under it.</summary>
    protected string Home { get; }

    protected string DriverConfigPath => Path.Combine(Home, "driver.json");

    protected string RemotesPath => Path.Combine(Home, "remotes.json");

    /// <summary>Where the harness wiring lands — which profile each harness runs as (D49 §4).</summary>
    protected string HarnessSettingsPath => Path.Combine(Home, "harnesses.json");

    /// <summary>Set an environment variable for this test, restoring it afterwards.</summary>
    protected void Redirect(string name, string? value)
    {
        if (!_restore.ContainsKey(name)) _restore[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    /// <summary>
    /// One request, through the module's real public door — never its protected router.
    /// </summary>
    /// <remarks>
    /// The response carries the refusal as well as the answer, which matters: a module that throws is
    /// a page that shows a toast, and "what does the person actually see when this goes wrong" is half
    /// of any contract worth testing.
    /// </remarks>
    protected static Task<IpcResponse> AskAsync(IIpcModule module, string type, object? payload = null) =>
        module.HandleMessageAsync(
            new IpcRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Module = module.ModuleName,
                Type = type,
                Payload = payload is null
                    ? null
                    : JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload)),
            },
            CancellationToken.None);

    /// <summary>
    /// A successful answer as the page receives it: serialized, then read back as JSON.
    /// </summary>
    /// <remarks>
    /// Deliberately not reflection over the anonymous type. What the page sees is the SERIALIZED
    /// shape — a renamed property, or one that stops serializing, is exactly the break worth catching,
    /// and reflection would sail straight past it. The camelCase the wire uses is the page's too.
    /// </remarks>
    protected static async Task<JsonElement> AnswerAsync(
        IIpcModule module, string type, object? payload = null)
    {
        var response = await AskAsync(module, type, payload);
        Assert.True(response.Success, $"{type} was refused: {response.Error?.Message}");
        return JsonSerializer.SerializeToElement(
            response.Data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    /// <summary>
    /// Everything a refusal carries, flattened — the code, the fallback message, and the parameters.
    /// </summary>
    /// <remarks>
    /// All three, because the framework treats <c>Message</c> as a dev-only fallback and the
    /// person-facing text is produced client-side from <c>Code</c> and <c>Parameters</c>. A module
    /// whose careful sentence lands only in <c>Message</c> is a module whose sentence the person never
    /// reads, and asserting on one field would hide which of those is happening.
    /// </remarks>
    protected static async Task<string> RefusalAsync(
        IIpcModule module, string type, object? payload = null)
    {
        var response = await AskAsync(module, type, payload);
        Assert.False(response.Success, $"{type} was expected to be refused, and was not");

        var error = response.Error;
        var parameters = error?.Parameters is { } bag
            ? string.Join(" ", bag.Select(entry => $"{entry.Key}={entry.Value}"))
            : "";
        return $"{error?.Code} {error?.Message} {parameters}".Trim();
    }

    public void Dispose()
    {
        foreach (var (name, value) in _restore) Environment.SetEnvironmentVariable(name, value);
        if (Directory.Exists(Home)) Directory.Delete(Home, recursive: true);
        GC.SuppressFinalize(this);
    }
}

