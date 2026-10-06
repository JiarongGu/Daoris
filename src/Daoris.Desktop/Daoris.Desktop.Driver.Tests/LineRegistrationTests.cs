using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A registration composed from a repository's line (WSSETUP5, D124 §3.3): what <c>connect</c> would send from that
/// manifest. The driver's half of a TWIN with the CLI's <c>connect.ts</c> (<c>registration()</c>, with the
/// <c>readManifest</c>, <c>isDeclared</c> and <c>readLanes</c> it runs first), whose <c>connect-twin.test.ts</c> holds
/// the same table, row for row and in the same order, and parses this theory to hold it to its own.
/// </summary>
/// <remarks>
/// <para>Each row is a manifest and a lanes file as the line holds them, the service the body goes to, and what
/// <c>connect</c> would do: the body it sends (<c>@root</c> is the checkout's root), <c>declares nothing</c> where it
/// refuses for want of a declaration, or <c>refused: manifest</c> or <c>refused: lanes</c> where it refuses the file.
/// The repository is <c>game</c> in every row.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class LineRegistrationTests
{
    private const string Root = "@root";

    [Theory]
    [InlineData("a declared manifest, to this machine's service: the root travels", """{"source":"daoris@0.0.1","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]}}""", null, "local", """{"repository":"game","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]},"join":false,"shareKnowledge":false,"lanes":[],"root":"@root"}""")]
    [InlineData("to a remote service: no root", """{"source":"daoris@0.0.1","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]}}""", null, "remote", """{"repository":"game","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("no packs is none", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("uses by its rule: trimmed, a repeat in any case, a blank and its own name dropped", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":[" engine","ENGINE","Game",""]}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"uses":["engine"]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("uses that names nothing is no field at all", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":["game","  "]}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("uses that is not a list is none", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":"engine"}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("a field the domain carries beyond the four travels as written", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"notes":"kept"}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"notes":"kept"},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("join and knowledge, as explicit booleans", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":{"join":true,"knowledge":true}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":true,"shareKnowledge":true,"lanes":[]}""")]
    [InlineData("join alone shares no knowledge", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":{"join":true}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":true,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("a remote of null is local", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":null}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("the lanes' words, trimmed, never their paths, the steward's mark explicit", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{"lanes":[{"id":"cli","title":" CLI ","summary":"The package","paths":["src/**"]},{"id":"records","title":"Records","paths":["TASKS.md"],"steward":true}]}""", "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[{"id":"cli","title":"CLI","summary":"The package","steward":false},{"id":"records","title":"Records","summary":"","steward":true}]}""")]
    [InlineData("a lanes file that names none is an explicit empty list", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{"lanes":[]}""", "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("a lanes file with no lanes field is none", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{}""", "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("owns alone declares", """{"source":"daoris@0.0.1","domain":{"summary":"","owns":["play"],"accepts":[]}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"","owns":["play"],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("no domain declares nothing", """{"source":"daoris@0.0.1","packs":[]}""", null, "local", "declares nothing")]
    [InlineData("a domain of blanks declares nothing", """{"source":"daoris@0.0.1","domain":{"summary":"  ","owns":[],"accepts":[]}}""", null, "local", "declares nothing")]
    [InlineData("a manifest that is not JSON is refused", """{"source":""", null, "local", "refused: manifest")]
    [InlineData("JSON that is not an object is refused", """[]""", null, "local", "refused: manifest")]
    [InlineData("no source is refused", """{"packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]}}""", null, "local", "refused: manifest")]
    [InlineData("knowledge without join is refused", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":{"knowledge":true}}""", null, "local", "refused: manifest")]
    [InlineData("a manifest refused and a lanes file refused: the manifest is said", """{"packs":[]}""", """{"lanes":""", "local", "refused: manifest")]
    [InlineData("a lanes file that is not JSON is refused whole", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{"lanes":""", "local", "refused: lanes")]
    [InlineData("a lanes file that is not an object is refused whole", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """[]""", "local", "refused: lanes")]
    [InlineData("a lane with no paths is refused whole", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{"lanes":[{"id":"cli","title":"CLI"}]}""", "local", "refused: lanes")]
    [InlineData("a lane id outside the alphabet is refused whole", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{"lanes":[{"id":"CLI","paths":["src/**"]}]}""", "local", "refused: lanes")]
    [InlineData("two stewards are refused whole", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}""", """{"lanes":[{"id":"a","paths":["a/**"],"steward":true},{"id":"b","paths":["b/**"],"steward":true}]}""", "local", "refused: lanes")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":["straße","STRASSE"]}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"uses":["straße","STRASSE"]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    [InlineData("a dotted capital I is not an i with a dot above", """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":["İzmir","i\u0307zmir"]}}""", null, "remote", """{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"uses":["İzmir","i\u0307zmir"]},"join":false,"shareKnowledge":false,"lanes":[]}""")]
    public void A_manifest_on_a_line_registers_as_connect_would_send_it(string name, string manifest, string? lanes, string service, string expected)
    {
        var composed = LineRegistration.Compose("game", manifest, lanes, Root, localService: service == "local");

        var answer = composed.ManifestProblem is not null ? "refused: manifest"
            : composed.LanesProblems.Count > 0 ? "refused: lanes"
            : !composed.Declares ? "declares nothing"
            : null;
        if (expected.StartsWith('{'))
        {
            Assert.True(answer is null, $"{name}: {answer} ({composed.ManifestProblem}{string.Join("; ", composed.LanesProblems)})");
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), composed.Body), $"{name}: {composed.Body!.ToJsonString()}");
        }
        else
        {
            Assert.Equal(expected, answer);
        }
    }

    [Fact]
    public void The_body_keeps_connects_order_of_fields()
    {
        var composed = LineRegistration.Compose(
            "game", """{"source":"daoris@0.0.1","domain":{"summary":"s","owns":[],"accepts":[]}}""", null, Root, localService: true);

        Assert.Equal(
            """{"repository":"game","packs":[],"domain":{"summary":"s","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[],"root":"@root"}""",
            composed.Body!.ToJsonString());
    }

    [Fact]
    public void A_manifest_that_declares_nothing_still_composes_the_rest()
    {
        var composed = LineRegistration.Compose(
            "game", """{"source":"daoris@0.0.1","packs":["web"],"remote":{"join":true}}""",
            """{"lanes":[{"id":"cli","paths":["src/**"]}]}""", Root, localService: true);

        Assert.False(composed.Declares);
        Assert.NotNull(composed.Body);
        Assert.Null(composed.Body!["domain"]);
        Assert.Equal("""["web"]""", composed.Body["packs"]!.ToJsonString());
        Assert.True(composed.Body["join"]!.GetValue<bool>());
        Assert.Equal("cli", composed.Body["lanes"]![0]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// Shapes <c>connect</c> would send on and the service would refuse, or that would stop <c>connect</c> before it sent
    /// anything: refused here as a manifest that does not read, naming the field, so nothing half-read is registered.
    /// </summary>
    [Theory]
    [InlineData("""{"source":"s","domain":"the game"}""", "domain")]
    [InlineData("""{"source":"s","domain":{"summary":7,"owns":[],"accepts":[]}}""", "domain.summary")]
    [InlineData("""{"source":"s","domain":{"summary":"s","owns":"play","accepts":[]}}""", "domain.owns")]
    [InlineData("""{"source":"s","domain":{"summary":"s","owns":[],"accepts":[1]}}""", "domain.accepts")]
    [InlineData("""{"source":"s","packs":"web","domain":{"summary":"s","owns":[],"accepts":[]}}""", "packs")]
    public void A_field_the_service_would_refuse_is_refused_naming_it(string manifest, string field)
    {
        var composed = LineRegistration.Compose("game", manifest, null, Root, localService: true);

        Assert.Null(composed.Body);
        Assert.Contains($"`{field}`", composed.ManifestProblem);
    }

    /// <summary>A list the domain leaves out is none, as the service reads it: sent as written, and declaring nothing by itself.</summary>
    [Fact]
    public void A_list_the_domain_leaves_out_is_none()
    {
        var said = LineRegistration.Compose("game", """{"source":"s","domain":{"summary":"s"}}""", null, Root, localService: false);
        var silent = LineRegistration.Compose("game", """{"source":"s","domain":{"owns":["play"]}}""", null, Root, localService: false);
        var empty = LineRegistration.Compose("game", """{"source":"s","domain":{}}""", null, Root, localService: false);

        Assert.True(said.Declares);
        Assert.Equal("""{"summary":"s"}""", said.Body!["domain"]!.ToJsonString());
        Assert.True(silent.Declares);
        Assert.False(empty.Declares);
    }

    [Fact]
    public void A_byte_order_mark_is_read_past_as_the_CLI_reads_one()
    {
        var composed = LineRegistration.Compose(
            "game", "﻿{\"source\":\"s\",\"domain\":{\"summary\":\"s\",\"owns\":[],\"accepts\":[]}}", "﻿{\"lanes\":[]}", Root, localService: false);

        Assert.Null(composed.ManifestProblem);
        Assert.Empty(composed.LanesProblems);
        Assert.True(composed.Declares);
    }

    [Fact]
    public void Every_lane_problem_is_said_not_only_the_first()
    {
        var composed = LineRegistration.Compose(
            "game", """{"source":"s","domain":{"summary":"s","owns":[],"accepts":[]}}""",
            """{"lanes":[{"id":"Bad","paths":["a/**"]},{"id":"ok"},{"id":"ok","paths":["b/**"]}]}""", Root, localService: true);

        Assert.Equal(
            [
                "lane 'Bad': its id must be lower-case letters, digits and dashes, starting with a letter",
                "lane 'ok': it has no paths",
                "lane 'ok': the id is used twice",
            ],
            composed.LanesProblems);
    }
}
