using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An update's plan says each interface entry added, removed or changed, compared whole by its name or id, and marks the ones that
/// ask more of the person: a command gaining <c>tool</c>, <c>writes</c>, <c>adopts</c> or an argument, a view gaining <c>context</c>,
/// storage newly declared.
/// </summary>
public sealed class PluginInterfaceChangesTests
{
    private static PluginManifest Manifest(string interfaces)
    {
        using var document = JsonDocument.Parse($$"""{ "interfaces": {{interfaces}} }""");
        return PluginManifest.Empty("acme-tickets") with { Interfaces = PluginInterfaces.Read(document.RootElement, hooks: true) };
    }

    private static string Rows(string was, string now) => string.Join(" / ", PluginChange.Between(Manifest(was), Manifest(now))
        .Select(change => $"{change.What} {change.Interface} {change.Name}{(change.AsksMore ? " asks more" : "")}: {change.Was} -> {change.Now}"));

    [Fact]
    public void The_same_interfaces_change_nothing()
    {
        const string Same = """{ "commands": [{ "name": "status", "about": "x" }], "storage": { "for": "y" }, "views": [{ "id": "a", "title": "A" }] }""";

        Assert.Equal("", Rows(Same, Same));
    }

    /// <summary>[case, the interfaces installed, the interfaces the update holds, its rows].</summary>
    [Theory]
    [InlineData("a command added that asks nothing more",
        """{ }""", """{ "commands": [{ "name": "status", "about": "Says." }] }""",
        "interfaces commands status:  -> status \"Says.\"")]
    [InlineData("a command removed",
        """{ "commands": [{ "name": "status", "about": "Says." }] }""", """{ }""",
        "interfaces commands status: status \"Says.\" -> ")]
    [InlineData("a command becoming a tool",
        """{ "commands": [{ "name": "status", "about": "Says." }] }""", """{ "commands": [{ "name": "status", "about": "Says.", "tool": true }] }""",
        "interfaces commands status asks more: status \"Says.\" -> status \"Says.\"; for agents")]
    [InlineData("a command that writes, added",
        """{ }""", """{ "commands": [{ "name": "start", "about": "Starts.", "writes": true, "confirm": "Now?" }] }""",
        "interfaces commands start asks more:  -> start \"Starts.\"; changes things; confirm \"Now?\"")]
    [InlineData("a command gaining an argument",
        """{ "commands": [{ "name": "start", "about": "S." }] }""", """{ "commands": [{ "name": "start", "about": "S.", "args": [{ "name": "stage", "type": "text", "required": true }] }] }""",
        "interfaces commands start asks more: start \"S.\" -> start \"S.\"; args stage (text, required)")]
    [InlineData("a command's words changed",
        """{ "commands": [{ "name": "start", "about": "S." }] }""", """{ "commands": [{ "name": "start", "about": { "en": "S.", "zh": "Q." } }] }""",
        "interfaces commands start: start \"S.\" -> start {\"en\":\"S.\",\"zh\":\"Q.\"}")]
    [InlineData("storage newly declared",
        """{ }""", """{ "storage": { "for": "The token." } }""",
        "interfaces storage  asks more:  -> \"The token.\"")]
    [InlineData("storage gaining its forget",
        """{ "commands": [{ "name": "logout", "about": "L." }], "storage": { "for": "T." } }""", """{ "commands": [{ "name": "logout", "about": "L." }], "storage": { "for": "T.", "forget": "logout" } }""",
        "interfaces storage : \"T.\" -> \"T.\"; forget logout")]
    [InlineData("a view gaining what it is told",
        """{ "views": [{ "id": "prs", "title": "PRs" }] }""", """{ "views": [{ "id": "prs", "title": "PRs", "context": ["session"], "where": ["side", "page"] }] }""",
        "interfaces views prs asks more: prs \"PRs\"; page; machine -> prs \"PRs\"; side, page; machine; told session")]
    public void Each_interface_entry_is_a_row_and_one_that_asks_more_is_marked(string @case, string was, string now, string rows)
    {
        Assert.True(rows == Rows(was, now), $"{@case}: {Rows(was, now)}");
    }
}
