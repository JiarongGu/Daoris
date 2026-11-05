using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A hosted service's pull-request API on this machine's loopback (D241 points 5 and 8): the calls a pull-request plugin makes with the
/// identity's token, answered as GitHub's REST API and Azure DevOps' answer them, so a plugin pointed at <see cref="Address"/> opens and
/// reads a pull request with no service, CLI or sign-in of anyone's. It admits only the tokens a test accepts and keeps none it is shown.
/// </summary>
/// <remarks>
/// <para>GitHub's shape: <c>POST /repos/{owner}/{repo}/pulls</c> with <c>title</c>, <c>head</c>, <c>base</c> and <c>body</c> (201, or 422
/// where one is open for the branch); <c>GET …/pulls/{number}</c>; <c>GET …/pulls?head={owner}:{branch}&amp;state=all</c>. A token as
/// <c>Bearer</c> or <c>token</c>; a refused one answers 401 <c>Bad credentials</c>.</para>
/// <para>Azure DevOps' shape: <c>POST /{org}/{project}/_apis/git/repositories/{repo}/pullrequests</c> with <c>sourceRefName</c>,
/// <c>targetRefName</c>, <c>title</c> and <c>description</c> (201, or 409 <c>TF401179</c> where one is active);
/// <c>GET …/pullrequests/{id}</c>; <c>GET …/pullrequests?searchCriteria.sourceRefName=…&amp;searchCriteria.status=all</c>. A token as
/// HTTP Basic with an empty user, or <c>Bearer</c>; a refused one answers 401.</para>
/// <para>A test moves a pull request as the service's people would: <see cref="Complete"/> merges it, <see cref="Abandon"/> closes it.
/// One per test class (<see cref="IClassFixture{TFixture}"/>); numbers count up across the class.</para>
/// </remarks>
public sealed partial class PullRequestApiHost : IAsyncLifetime
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<string> _tokens = new(StringComparer.Ordinal);
    private readonly List<HostedPullRequest> _pulls = [];
    private readonly List<PullRequestApiCall> _calls = [];
    private Task? _serving;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The API's base address, as a plugin is pointed at it.</summary>
    public string Address => $"http://127.0.0.1:{Port}";

    /// <summary>Every pull request opened, as it stands now.</summary>
    public IReadOnlyList<HostedPullRequest> PullRequests
    {
        get { lock (_pulls) return [.. _pulls]; }
    }

    /// <summary>Every call answered, in order, with whether its token was admitted.</summary>
    public IReadOnlyList<PullRequestApiCall> Calls
    {
        get { lock (_calls) return [.. _calls]; }
    }

    public Task InitializeAsync()
    {
        _listener.Start();
        _serving = ServeAsync();
        return Task.CompletedTask;
    }

    /// <summary>Admit <paramref name="token"/> on every call from now on.</summary>
    public void Accept(string token)
    {
        lock (_tokens) _tokens.Add(token);
    }

    /// <summary>The pull request merged by the service, as <paramref name="commit"/>.</summary>
    public void Complete(int id, string commit) => Move(id, pull => pull with { State = HostedPullRequest.Merged, MergeCommit = commit });

    /// <summary>The pull request closed unmerged by the service.</summary>
    public void Abandon(int id) => Move(id, pull => pull with { State = HostedPullRequest.Closed });

    private void Move(int id, Func<HostedPullRequest, HostedPullRequest> change)
    {
        lock (_pulls)
        {
            var at = _pulls.FindIndex(pull => pull.Id == id);
            if (at < 0) throw new InvalidOperationException($"no pull request {id} on the stand-in API");
            _pulls[at] = change(_pulls[at]);
        }
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(client));
        }
    }

    private async Task AnswerAsync(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        try
        {
            if (LoopbackHttpRequest.Read(stream) is not { } request) return;
            var admitted = Admitted(request.Headers.GetValueOrDefault("Authorization"));
            lock (_calls) _calls.Add(new PullRequestApiCall(request.Method, request.Target, admitted));
            var (status, answer) = admitted ? Route(request) : Unauthorized(request.Path);
            var body = Encoding.UTF8.GetBytes(answer.ToJsonString());
            var head = $"HTTP/1.1 {status} {Reason(status)}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\n"
                + "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
            await stream.WriteAsync(body);
            await stream.FlushAsync();
        }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
        {
            // The client went first.
        }
    }

    /// <summary>Whether the call carries an accepted token: <c>Bearer</c>, <c>token</c>, or Basic with the token as its password.</summary>
    private bool Admitted(string? authorization)
    {
        if (authorization is null) return false;
        var space = authorization.IndexOf(' ');
        if (space < 0) return false;
        var scheme = authorization[..space];
        var value = authorization[(space + 1)..].Trim();
        string? token = scheme.ToLowerInvariant() switch
        {
            "bearer" or "token" => value,
            "basic" => Password(value),
            _ => null,
        };
        lock (_tokens) return token is not null && _tokens.Contains(token);
    }

    private static string? Password(string encoded)
    {
        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return text.IndexOf(':') is var colon and >= 0 ? text[(colon + 1)..] : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static (int, JsonNode) Unauthorized(string path) => (401, new JsonObject
    {
        ["message"] = path.StartsWith("/repos/", StringComparison.Ordinal)
            ? "Bad credentials"
            : "TF400813: The user is not authorized to access this resource.",
    });

    private (int, JsonNode) Route(LoopbackHttpRequest request)
    {
        if (GitHubPulls().Match(request.Path) is { Success: true } github)
            return GitHub(request, github.Groups["owner"].Value + "/" + github.Groups["repo"].Value, github.Groups["id"]);
        if (AzurePulls().Match(request.Path) is { Success: true } azure)
            return Azure(request, $"{azure.Groups["org"].Value}/{azure.Groups["project"].Value}/{azure.Groups["repo"].Value}", azure.Groups["id"]);
        return (404, new JsonObject { ["message"] = "Not Found" });
    }

    // ── GitHub's shape ─────────────────────────────────────────────────────────────────────────────

    private (int, JsonNode) GitHub(LoopbackHttpRequest request, string repository, Group id)
    {
        if (id.Success)
        {
            return request.Method == "GET" && Find(HostedPullRequest.GitHub, repository, int.Parse(id.Value)) is { } found
                ? (200, GitHubOf(found))
                : (404, new JsonObject { ["message"] = "Not Found" });
        }

        if (request.Method == "GET")
        {
            var query = Query(request.Query);
            var head = query.GetValueOrDefault("head") is { } named ? named[(named.IndexOf(':') + 1)..] : null;
            var state = query.GetValueOrDefault("state") ?? "open";
            var listed = Listed(HostedPullRequest.GitHub, repository)
                .Where(pull => head is null || pull.Head == head)
                .Where(pull => state == "all" || (state == "open") == (pull.State == HostedPullRequest.Open));
            return (200, new JsonArray([.. listed.Select(GitHubOf)]));
        }

        if (request.Method != "POST") return (404, new JsonObject { ["message"] = "Not Found" });
        var body = Body(request);
        var (title, headRef, baseRef) = (Text(body, "title"), Text(body, "head"), Text(body, "base"));
        if (title is null || headRef is null || baseRef is null)
            return (422, new JsonObject { ["message"] = "Validation Failed", ["errors"] = new JsonArray(new JsonObject { ["code"] = "missing_field" }) });
        headRef = headRef[(headRef.IndexOf(':') + 1)..];
        if (Listed(HostedPullRequest.GitHub, repository).Any(pull => pull.Head == headRef && pull.Base == baseRef && pull.State == HostedPullRequest.Open))
        {
            return (422, new JsonObject
            {
                ["message"] = "Validation Failed",
                ["errors"] = new JsonArray(new JsonObject
                {
                    ["resource"] = "PullRequest",
                    ["code"] = "custom",
                    ["message"] = $"A pull request already exists for {repository.Split('/')[0]}:{headRef}.",
                }),
            });
        }

        return (201, GitHubOf(Open(HostedPullRequest.GitHub, repository, headRef, baseRef, title, Text(body, "body"))));
    }

    private JsonObject GitHubOf(HostedPullRequest pull) => new()
    {
        ["number"] = pull.Id,
        ["state"] = pull.State == HostedPullRequest.Open ? "open" : "closed",
        ["merged"] = pull.State == HostedPullRequest.Merged,
        ["merge_commit_sha"] = pull.MergeCommit,
        ["title"] = pull.Title,
        ["body"] = pull.Body,
        ["url"] = $"{Address}/repos/{pull.Repository}/pulls/{pull.Id}",
        ["html_url"] = $"{Address}/{pull.Repository}/pull/{pull.Id}",
        ["head"] = new JsonObject { ["ref"] = pull.Head },
        ["base"] = new JsonObject { ["ref"] = pull.Base },
    };

    // ── Azure DevOps' shape ────────────────────────────────────────────────────────────────────────

    private (int, JsonNode) Azure(LoopbackHttpRequest request, string repository, Group id)
    {
        if (id.Success)
        {
            return request.Method == "GET" && Find(HostedPullRequest.AzureDevOps, repository, int.Parse(id.Value)) is { } found
                ? (200, AzureOf(found))
                : (404, new JsonObject { ["message"] = $"TF401180: The requested pull request was not found." });
        }

        if (request.Method == "GET")
        {
            var query = Query(request.Query);
            var source = query.GetValueOrDefault("searchCriteria.sourceRefName");
            var status = query.GetValueOrDefault("searchCriteria.status") ?? "active";
            var listed = Listed(HostedPullRequest.AzureDevOps, repository)
                .Where(pull => source is null || "refs/heads/" + pull.Head == source)
                .Where(pull => status == "all" || AzureStatus(pull) == status)
                .Select(AzureOf).ToList();
            return (200, new JsonObject { ["value"] = new JsonArray([.. listed]), ["count"] = listed.Count });
        }

        if (request.Method != "POST") return (404, new JsonObject { ["message"] = "Not Found" });
        var body = Body(request);
        var (title, source2, target) = (Text(body, "title"), Text(body, "sourceRefName"), Text(body, "targetRefName"));
        if (title is null || source2 is null || target is null)
            return (400, new JsonObject { ["message"] = "TF400898: An Internal Error Occurred: a pull request needs a title, a source and a target." });
        var (head, @base) = (Branch(source2), Branch(target));
        if (Listed(HostedPullRequest.AzureDevOps, repository).Any(pull => pull.Head == head && pull.Base == @base && pull.State == HostedPullRequest.Open))
        {
            return (409, new JsonObject
            {
                ["message"] = "TF401179: An active pull request for the source and target branch already exists.",
                ["typeKey"] = "GitPullRequestExistsException",
            });
        }

        return (201, AzureOf(Open(HostedPullRequest.AzureDevOps, repository, head, @base, title, Text(body, "description"))));
    }

    private static string Branch(string reference) => reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference[11..] : reference;

    private static string AzureStatus(HostedPullRequest pull) => pull.State switch
    {
        HostedPullRequest.Merged => "completed",
        HostedPullRequest.Closed => "abandoned",
        _ => "active",
    };

    private JsonObject AzureOf(HostedPullRequest pull)
    {
        var parts = pull.Repository.Split('/');
        var answer = new JsonObject
        {
            ["pullRequestId"] = pull.Id,
            ["status"] = AzureStatus(pull),
            ["title"] = pull.Title,
            ["description"] = pull.Body,
            ["sourceRefName"] = "refs/heads/" + pull.Head,
            ["targetRefName"] = "refs/heads/" + pull.Base,
            ["mergeStatus"] = pull.State == HostedPullRequest.Merged ? "succeeded" : "queued",
            ["url"] = $"{Address}/{parts[0]}/{parts[1]}/_apis/git/repositories/{parts[2]}/pullRequests/{pull.Id}",
            ["repository"] = new JsonObject { ["name"] = parts[2], ["project"] = new JsonObject { ["name"] = parts[1] } },
        };
        if (pull.MergeCommit is not null) answer["lastMergeCommit"] = new JsonObject { ["commitId"] = pull.MergeCommit };
        return answer;
    }

    // ── The record ─────────────────────────────────────────────────────────────────────────────────

    private HostedPullRequest Open(string platform, string repository, string head, string @base, string title, string? body)
    {
        lock (_pulls)
        {
            var pull = new HostedPullRequest(_pulls.Count + 1, platform, repository, head, @base, title, body, HostedPullRequest.Open, null);
            _pulls.Add(pull);
            return pull;
        }
    }

    private HostedPullRequest? Find(string platform, string repository, int id) =>
        Listed(platform, repository).FirstOrDefault(pull => pull.Id == id);

    private List<HostedPullRequest> Listed(string platform, string repository)
    {
        lock (_pulls) return [.. _pulls.Where(pull => pull.Platform == platform && pull.Repository == repository)];
    }

    private static JsonObject? Body(LoopbackHttpRequest request)
    {
        try
        {
            return JsonNode.Parse(request.Body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonObject? body, string name) =>
        body?[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static Dictionary<string, string> Query(string query) => query.Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .GroupBy(pair => Uri.UnescapeDataString(pair[0]), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => Uri.UnescapeDataString(group.Last().ElementAtOrDefault(1) ?? ""), StringComparer.Ordinal);

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        400 => "Bad Request",
        401 => "Unauthorized",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Unprocessable Entity",
    };

    [GeneratedRegex(@"^/repos/(?<owner>[^/]+)/(?<repo>[^/]+)/pulls(?:/(?<id>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubPulls();

    [GeneratedRegex(@"^/(?<org>[^/]+)/(?<project>[^/]+)/_apis/git/repositories/(?<repo>[^/]+)/pullrequests(?:/(?<id>\d+))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AzurePulls();

    public async Task DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        if (_serving is not null) await _serving;
        _stop.Dispose();
    }
}

/// <summary>A pull request on the stand-in API: its number, its service's shape, its repository's path, branches, words and state.</summary>
public sealed record HostedPullRequest(
    int Id, string Platform, string Repository, string Head, string Base, string Title, string? Body, string State, string? MergeCommit)
{
    public const string GitHub = "github";
    public const string AzureDevOps = "azure-devops";
    public const string Open = "open";
    public const string Merged = "merged";
    public const string Closed = "closed";
}

/// <summary>A call the stand-in API answered: its method, its target, and whether its token was admitted (never the token).</summary>
public sealed record PullRequestApiCall(string Method, string Target, bool Admitted);
