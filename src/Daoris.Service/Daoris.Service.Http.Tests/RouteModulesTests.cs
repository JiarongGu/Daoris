using Daoris.Contracts;
using Daoris.Knowledge.Http.Routes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The one binder's reflection: the host finds its route modules in its own assembly, and every route the contracts declare is
/// mapped once, at its declared method and path. A request that does not bind is answered as the framework's binding answered
/// one, 400 with no body, and a handler's own refusal keeps its sentence.
/// </summary>
public sealed class RouteModulesTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public void The_host_finds_its_modules_by_reflection()
    {
        var modules = RouteModules.In(typeof(HostWiring).Assembly);

        Assert.Contains(typeof(KnowledgeRoutes), modules);
        Assert.All(modules, module => Assert.NotNull(module.GetConstructor([typeof(HostWiring)])));
    }

    [Fact]
    public void Every_declared_route_is_mapped_once_at_its_method_and_path()
    {
        // The feed's doors are a shared host's alone (FeedRoutes maps none on a local one), so this local host maps none of them.
        var declared = RouteDeclaration.In(typeof(IRoute<>).Assembly)
            .Where(route => route.Request.Namespace != "Daoris.Contracts.Feed").ToList();
        var mapped = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Method: method, Pattern: endpoint.RoutePattern.RawText ?? "")))
            .ToList();

        Assert.NotEmpty(declared);
        Assert.All(declared, route => Assert.Single(mapped, each => each.Method == route.Method && each.Pattern == route.Path));
    }

    [Theory]
    [InlineData("/api/search")]
    [InlineData("/api/search?q=words&limit=many")]
    [InlineData("/api/search?q=words&localOnly=perhaps")]
    [InlineData("/api/entry")]
    public async Task A_request_that_does_not_bind_is_answered_400_with_no_body(string path)
    {
        var answer = await host.GetAsync(path);

        Assert.Equal(400, answer.Status);
        Assert.Equal("", answer.Body);
    }

    [Fact]
    public void The_quest_doors_are_a_module_of_their_own()
    {
        Assert.Contains(typeof(QuestRoutes), RouteModules.In(typeof(HostWiring).Assembly));
    }

    [Fact]
    public void The_person_s_quest_doors_are_a_module_of_their_own()
    {
        Assert.Contains(typeof(QuestPersonRoutes), RouteModules.In(typeof(HostWiring).Assembly));
    }

    /// <summary>A body is bound as the framework bound a body parameter: required unless marked optional, and JSON or refused 415.</summary>
    [Fact]
    public async Task A_required_body_left_out_is_400_and_an_optional_one_binds_as_naming_nothing()
    {
        var missing = await host.SendAsync("POST", "/api/quests/nosuchquest/respond", DaorisHost.Loopback);
        Assert.Equal((400, ""), (missing.Status, missing.Body));

        var nulled = await host.SendAsync("POST", "/api/quests/nosuchquest/respond", DaorisHost.Loopback, json: "null");
        Assert.Equal((400, ""), (nulled.Status, nulled.Body));

        // A done's words are optional, so it reaches the exchange, which says the quest is not one it holds.
        var done = await host.SendAsync("POST", "/api/quests/nosuchquest/done", DaorisHost.Loopback);
        Assert.Equal(404, done.Status);
        Assert.False(string.IsNullOrEmpty(done.Error));
    }

    [Fact]
    public async Task A_json_request_that_sends_no_bytes_binds_as_naming_nothing()
    {
        var done = await host.SendAsync("POST", "/api/quests/nosuchquest/done", DaorisHost.Loopback, json: "");
        Assert.Equal(404, done.Status);

        var missing = await host.SendAsync("POST", "/api/quests/nosuchquest/respond", DaorisHost.Loopback, json: "");
        Assert.Equal((400, ""), (missing.Status, missing.Body));
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_415_and_a_door_that_binds_none_never_reads_it()
    {
        var plain = await SendTextAsync("/api/quests/nosuchquest/respond", "action=take");
        Assert.Equal((415, ""), plain);

        // The yes binds no body, so whatever is sent is left unread and the exchange answers.
        var (status, _) = await SendTextAsync("/api/quests/nosuchquest/accept", "anything");
        Assert.Equal(404, status);
    }

    private async Task<(int Status, string Body)> SendTextAsync(string path, string text)
    {
        var context = await host.Server.SendAsync(http =>
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            http.Request.Method = "POST";
            http.Request.Path = path;
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentType = "text/plain";
            http.Request.ContentLength = bytes.Length;
            http.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new Bodied());
        });
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class Bodied : Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    [Fact]
    public async Task A_handler_s_refusal_keeps_its_sentence_and_a_bound_read_answers()
    {
        var blank = await host.GetAsync("/api/search?q=%20");
        Assert.Equal(400, blank.Status);
        Assert.Equal("q is required", blank.Error);

        var hits = await host.GetAsync("/api/search?Q=words&LIMIT=3&localOnly=FALSE");
        Assert.Equal(200, hits.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, hits.Json.ValueKind);
    }
}
