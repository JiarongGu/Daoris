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
        var declared = RouteDeclaration.In(typeof(IRoute<>).Assembly);
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
