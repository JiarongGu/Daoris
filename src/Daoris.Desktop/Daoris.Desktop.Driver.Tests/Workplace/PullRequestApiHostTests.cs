using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The stand-in pull-request API (<see cref="PullRequestApiHost"/>) answers as each service's REST API does, for the plugins that will
/// call it (the git design's G9): a refused token opens nothing and says so in the service's words; a second pull request for one branch
/// is refused as the service refuses it; Azure DevOps' token as Basic with an empty user, its list by source branch, and an abandon read.
/// </summary>
/// <remarks><para>In the Process half, beside a listener.</para></remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PullRequestApiHostTests(PullRequestApiHost api) : IClassFixture<PullRequestApiHost>
{
    private readonly string _token = "fixture-api-token-" + Guid.NewGuid().ToString("N")[..8];

    private HttpClient Client(AuthenticationHeaderValue? authorization)
    {
        var http = new HttpClient { BaseAddress = new Uri(api.Address) };
        http.DefaultRequestHeaders.Authorization = authorization;
        return http;
    }

    [Fact]
    public async Task On_GitHubs_shape_a_refused_token_opens_nothing_and_a_second_pull_request_for_a_branch_is_refused()
    {
        api.Accept(_token);
        var repository = "org-" + Guid.NewGuid().ToString("N")[..6] + "/engine";
        var opening = new { title = "Fix", head = "work/fix", @base = "main" };
        using (var refused = Client(new AuthenticationHeaderValue("Bearer", "not-the-token")))
        {
            var answer = await refused.PostAsJsonAsync($"/repos/{repository}/pulls", opening);
            Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
            Assert.Equal("Bad credentials", (await answer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        }

        Assert.DoesNotContain(api.PullRequests, pull => pull.Repository == repository);
        Assert.Contains(api.Calls, call => !call.Admitted && call.Target.Contains(repository, StringComparison.Ordinal));

        using var http = Client(new AuthenticationHeaderValue("token", _token));
        Assert.Equal(HttpStatusCode.Created, (await http.PostAsJsonAsync($"/repos/{repository}/pulls", opening)).StatusCode);
        var again = await http.PostAsJsonAsync($"/repos/{repository}/pulls", opening);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Contains("already exists", await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var listed = await http.GetFromJsonAsync<JsonElement>($"/repos/{repository}/pulls?head={repository.Split('/')[0]}:work/fix&state=all");
        Assert.Equal(1, listed.GetArrayLength());
    }

    [Fact]
    public async Task On_Azure_DevOps_shape_a_token_as_Basic_opens_lists_and_reads_an_abandon()
    {
        api.Accept(_token);
        var project = "project-" + Guid.NewGuid().ToString("N")[..6];
        var pulls = $"/org/{project}/_apis/git/repositories/engine/pullrequests";
        using var http = Client(new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + _token))));

        var opened = await http.PostAsJsonAsync(pulls + "?api-version=7.1",
            new { sourceRefName = "refs/heads/work/fix", targetRefName = "refs/heads/main", title = "Fix", description = "Why." });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var id = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pullRequestId").GetInt32();
        var again = await http.PostAsJsonAsync(pulls + "?api-version=7.1",
            new { sourceRefName = "refs/heads/work/fix", targetRefName = "refs/heads/main", title = "Fix" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("TF401179", await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var listed = await http.GetFromJsonAsync<JsonElement>(
            pulls + "?searchCriteria.sourceRefName=refs%2Fheads%2Fwork%2Ffix&searchCriteria.status=all&api-version=7.1");
        Assert.Equal(1, listed.GetProperty("count").GetInt32());

        api.Abandon(id);
        var read = await http.GetFromJsonAsync<JsonElement>($"{pulls}/{id}?api-version=7.1");
        Assert.Equal("abandoned", read.GetProperty("status").GetString());
        Assert.False(read.TryGetProperty("lastMergeCommit", out _));
    }
}
