using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service's ledger as Ask Daoris's checkout reads ask it: the session records, the registry and the live sessions the test names,
/// and every path asked, in order.
/// </summary>
internal sealed class HelpLedgerStandIn : HttpMessageHandler
{
    private readonly List<string> _asked = [];

    public (string Id, string Repository, string Workspace, string? Tree)[] Records { get; set; } = [];

    public (string Repository, string Workspace, string Root)[] Registry { get; set; } = [];

    public (string Id, string State, string Tree)[] Live { get; set; } = [];

    public IReadOnlyList<string> Asked
    {
        get { lock (_asked) return [.. _asked]; }
    }

    public ServiceClient? Client() => new("http://stand-in", null, new HttpClient(new SessionChanges(this), disposeHandler: false));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        lock (_asked) _asked.Add(uri.AbsolutePath);
        object body = uri.AbsolutePath switch
        {
            "/api/registry" => Registry.Select(row => new { repository = row.Repository, workspace = row.Workspace, root = row.Root }).ToArray(),
            "/api/sessions" when uri.Query.Contains("includeClosed", StringComparison.Ordinal) =>
                Records.Select(each => new { id = each.Id, repository = each.Repository, state = "working", workspace = each.Workspace, tree = each.Tree })
                    .Concat(Live.Select(each => new { id = each.Id, repository = "engine", state = each.State, workspace = "aurora", tree = (string?)each.Tree }))
                    .ToArray(),
            "/api/sessions" => Live.Select(each => new { id = each.Id, repository = "engine", state = each.State, tree = each.Tree }).ToArray(),
            _ => Array.Empty<object>(),
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        });
    }
}
