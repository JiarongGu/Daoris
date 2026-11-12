using System.Net;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The service's stand-in answers each named path its body as JSON, any other the fallback, and records each request.</summary>
public sealed class RoutedHandlerTests
{
    [Fact]
    public async Task A_named_path_answers_its_body_and_any_other_the_fallback()
    {
        using var client = new HttpClient(new RoutedHandler(("/api/sessions", """[{"id":"s-1"}]"""))) { BaseAddress = new("http://stand-in") };

        var named = await client.GetAsync("/api/sessions?kind=chat");
        var other = await client.GetAsync("/api/quests");

        Assert.Equal(HttpStatusCode.OK, named.StatusCode);
        Assert.Equal("application/json", named.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""[{"id":"s-1"}]""", await named.Content.ReadAsStringAsync());
        Assert.Equal("[]", await other.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Each_request_is_recorded_with_its_method_path_and_body()
    {
        var handler = new RoutedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("http://stand-in") };

        await client.GetAsync("/api/registry");
        await client.PostAsync("/api/sessions/s-1/say", new StringContent("""{"text":"go"}""", Encoding.UTF8, "application/json"));

        Assert.Equal([("GET", "/api/registry", ""), ("POST", "/api/sessions/s-1/say", """{"text":"go"}""")], handler.Requests);
    }

    [Fact]
    public void A_registry_lists_each_repository_with_what_it_was_given()
    {
        Assert.Equal(
            """[{"repository":"game","workspace":"aurora"},{"repository":"engine","workspace":"aurora","root":"checkouts\\engine"},{"repository":"tools"}]""",
            RoutedHandler.Registry(("game", "aurora", null), ("engine", "aurora", @"checkouts\engine"), ("tools", null, null)));
    }
}
