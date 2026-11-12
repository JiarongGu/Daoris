using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service as a test stands it in over HTTP: each named path answers its body as JSON, any other path answers
/// <see cref="Otherwise"/> (an empty list unless a test says), and every request is kept with its method, path and body.
/// A test's own stand-in derives from it with its routes; one that decides by more than the path keeps its own handler.
/// Linked into the modules' tests.
/// </summary>
internal class RoutedHandler(params (string Path, string Body)[] routes) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Present = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly Dictionary<string, string> _routes = routes.ToDictionary(route => route.Path, route => route.Body, StringComparer.Ordinal);

    private readonly List<(string Method, string Path, string Body)> _requests = [];

    /// <summary>What a path no route names answers.</summary>
    public (HttpStatusCode Status, string Body) Otherwise { get; init; } = (HttpStatusCode.OK, "[]");

    /// <summary>Every request so far, in order.</summary>
    public IReadOnlyList<(string Method, string Path, string Body)> Requests
    {
        get { lock (_requests) return [.. _requests]; }
    }

    /// <summary>The body of <c>/api/registry</c>: each repository with its workspace and root, a missing one left out.</summary>
    public static string Registry(params (string Repository, string? Workspace, string? Root)[] entries) =>
        JsonSerializer.Serialize(entries.Select(entry => new Entry(entry.Repository, entry.Workspace, entry.Root)), Present);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        lock (_requests) _requests.Add((request.Method.Method, path, body));
        var (status, answer) = _routes.TryGetValue(path, out var named) ? (HttpStatusCode.OK, named) : Otherwise;
        return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }

    private sealed record Entry(
        [property: JsonPropertyName("repository")] string Repository,
        [property: JsonPropertyName("workspace")] string? Workspace,
        [property: JsonPropertyName("root")] string? Root);
}
