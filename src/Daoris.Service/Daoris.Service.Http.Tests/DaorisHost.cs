using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

// 🔴 The host reads its configuration from the PROCESS environment (DAORIS_MODE, DAORIS_HOME, …), some of
// it lazily after start — the remotes map on every sync, the quest keeper at composition — so two hosts
// with different machines cannot run at once in one process. Serial, then: each class's host owns the
// environment for exactly its own lifetime (HTTP1).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Daoris.Service.Http.Tests;

/// <summary>A caller's answer: the status, the body as text, and its content type.</summary>
public sealed record Answer(int Status, string Body, string? ContentType)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    /// <summary>The error sentence of a refusal — every refusal's one field.</summary>
    public string Error => Json.GetProperty("error").GetString()!;
}

/// <summary>
/// Sets the process environment for one host's lifetime and puts back what was there — including the
/// variables this host must NOT inherit from the machine running the tests.
/// </summary>
/// <remarks>
/// A developer's machine may carry an installed Daoris's <c>DAORIS_HOME</c> for the account (D63), and
/// a remote's URL and key. Inherited, a test host would write into that home and a delete would push to
/// that remote — so every variable the host reads is named here, set or cleared, never left to chance.
/// </remarks>
public sealed class ScopedEnvironment : IDisposable
{
    private readonly Dictionary<string, string?> _before = new();

    public ScopedEnvironment(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (name, value) in values)
        {
            _before[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _before) Environment.SetEnvironmentVariable(name, value);
    }
}

/// <summary>
/// The HTTP host's real <c>Program</c>, in-process, over a machine of its own: a scratch home, index,
/// folder of repositories and web root. Nothing binds a port — the in-memory server replaces Kestrel —
/// so a Daoris already running on this machine is never reached.
/// </summary>
public class DaorisHost : IDisposable
{
    /// <summary>A caller on this machine, by its real loopback address rather than the test server's none.</summary>
    public static readonly IPAddress Loopback = IPAddress.Loopback;

    /// <summary>A caller off the machine: TEST-NET-3, an address that is never anyone's (RFC 5737).</summary>
    public static readonly IPAddress OffMachine = IPAddress.Parse("203.0.113.9");

    /// <summary>What the page says, so an answer can be searched for it.</summary>
    public const string PageMarker = "daoris-http1-page";

    private readonly ScopedEnvironment _environment;
    private readonly Factory _factory;

    /// <param name="environment">Variables to set over the host's own, for a test of what the host reads at start.</param>
    public DaorisHost(
        ServiceMode mode, IReadOnlyDictionary<string, string?>? settings = null, Action<string>? seed = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        Scratch = Path.Combine(Path.GetTempPath(), "daoris-http1-" + Guid.NewGuid().ToString("N")[..8]);
        Home = Path.Combine(Scratch, "home");
        Repositories = Path.Combine(Scratch, "repositories");
        Database = Path.Combine(Scratch, "index", "knowledge.db");
        WebRoot = Path.Combine(Scratch, "wwwroot");
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repositories);
        // A page to serve, so "no page is served" is a refusal and not an absence.
        Directory.CreateDirectory(WebRoot);
        File.WriteAllText(Path.Combine(WebRoot, "index.html"), $"<!doctype html><title>{PageMarker}</title>");
        seed?.Invoke(Repositories);

        var variables = new Dictionary<string, string?>
        {
            [Access.ModeVariable] = mode == ServiceMode.Shared ? "shared" : null,
            [Access.WorkspaceVariable] = null,
            [DaorisHome.Variable] = Home,
            [ServiceOptions.DatabaseVariable] = Database,
            [ServiceOptions.RootVariable] = Repositories,
            // No model: every answer here is the lexical tier's, as a model-less deployment's is (D24).
            [ServiceOptions.ModelVariable] = null,
            [ServiceOptions.UrlVariable] = null,
            [ServiceOptions.WindowVariable] = null,
            [RemoteConfig.UrlVariable] = null,
            [RemoteConfig.KeyVariable] = null,
            [RemoteConfig.WorkspaceVariable] = null,
            [RemoteConfig.PathVariable] = null,
            [RuleProposalBox.HomeVariable] = null,
            [IntakeScope.AskVariable] = null,
            [IntakeScope.SessionVariable] = null,
            ["DAORIS_WEB_ORIGIN"] = null,
            ["ASPNETCORE_URLS"] = null,
            // An in-process host would watch the test runner's own input (LOG2a).
            [Daoris.Knowledge.Http.InputEndStop.Variable] = null,
        };
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>()) variables[name] = value;
        _environment = new ScopedEnvironment(variables);

        _factory = new Factory(WebRoot, settings ?? new Dictionary<string, string?>());
        try
        {
            // Start it now, while this host owns the environment: the entry point reads it on the way up.
            _ = _factory.Server;
        }
        catch
        {
            // A host that refused to start: the environment goes back, and so does the scratch.
            _factory.Dispose();
            _environment.Dispose();
            Sweep();
            throw;
        }
    }

    public string Scratch { get; }

    public string Home { get; }

    public string Repositories { get; }

    public string Database { get; }

    public string WebRoot { get; }

    public TestServer Server => _factory.Server;

    public IServiceProvider Services => _factory.Services;

    /// <summary>The service the host composed — the same store its doors answer from.</summary>
    public ComposedService Composed => Services.GetRequiredService<ComposedService>();

    /// <summary>Every route the host mapped, as (method, pattern).</summary>
    public IReadOnlyList<(string Method, string Pattern)> Routes() =>
        Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (method, endpoint.RoutePattern.RawText ?? "")))
            .Distinct()
            .OrderBy(route => route.Item2, StringComparer.Ordinal)
            .ThenBy(route => route.method, StringComparer.Ordinal)
            .ToList();

    /// <summary>One request, from <paramref name="from"/>, with a bearer key and a JSON body when given.</summary>
    public async Task<Answer> SendAsync(
        string method, string pathAndQuery, IPAddress from, string? key = null, string? json = null)
    {
        var query = pathAndQuery.IndexOf('?');
        var context = await Server.SendAsync(http =>
        {
            http.Request.Method = method;
            http.Request.Path = query < 0 ? pathAndQuery : pathAndQuery[..query];
            http.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(pathAndQuery[query..]);
            http.Connection.RemoteIpAddress = from;
            if (key is not null) http.Request.Headers.Authorization = "Bearer " + key;
            if (json is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                http.Request.Body = new MemoryStream(bytes);
                http.Request.ContentType = "application/json";
                http.Request.ContentLength = bytes.Length;
                // A context built by hand says it can carry no body, and a route would bind none.
                http.Features.Set<IHttpRequestBodyDetectionFeature>(new HasBody());
            }
        });

        using var reader = new StreamReader(context.Response.Body);
        return new Answer(context.Response.StatusCode, await reader.ReadToEndAsync(), context.Response.ContentType);
    }

    public Task<Answer> GetAsync(string pathAndQuery, IPAddress? from = null, string? key = null) =>
        SendAsync("GET", pathAndQuery, from ?? Loopback, key);

    public Task<Answer> PostAsync(string path, object body, IPAddress? from = null, string? key = null) =>
        SendAsync("POST", path, from ?? Loopback, key, JsonSerializer.Serialize(body));

    public Task<Answer> DeleteAsync(string path, IPAddress? from = null, string? key = null) =>
        SendAsync("DELETE", path, from ?? Loopback, key);

    /// <summary>Every line this host's machine log holds, across its files.</summary>
    public IReadOnlyList<string> LogLines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.host.jsonl")
            .SelectMany(path =>
            {
                // The host holds the file open for appending; read beside it.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .ToList();
    }

    public void Dispose()
    {
        // The host registers the service it composed as an instance, which the container does not own
        // and so never disposes: in a deployment the process ends instead. Here the next host follows.
        var composed = Composed;
        _factory.Dispose();
        composed.DisposeAsync().AsTask().GetAwaiter().GetResult();
        SqliteConnection.ClearAllPools();
        _environment.Dispose();
        Sweep();
        GC.SuppressFinalize(this);
    }

    private void Sweep()
    {
        // The entry point closes its log a moment after the host stops; a few short tries, then leave it
        // to the OS's temp sweep rather than fail a test over a handle.
        for (var attempt = 0; attempt < 20 && Directory.Exists(Scratch); attempt++)
        {
            try
            {
                Directory.Delete(Scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private sealed class HasBody : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    /// <summary>
    /// The factory's only additions: the deployed environment and the web root. Nothing is replaced —
    /// the routes, the gate and the store are the host's own.
    /// </summary>
    private sealed class Factory(string webRoot, IReadOnlyDictionary<string, string?> settings)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // A published host runs as Production; the factory's default of Development would add the
            // exception page and static web assets, neither of which a deployment has.
            builder.UseEnvironment("Production");
            builder.UseSetting(WebHostDefaults.WebRootKey, webRoot);
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
        }
    }
}
