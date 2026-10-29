using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f1 (D207, the integration design §5.3): the failure reader and the signature, the driver's twin of
/// <c>tools/test-failures.mjs</c>. Held cell for cell to ONE table, <c>fixtures/failure-signatures.json</c>, which
/// <c>src/Daoris.Cli/tool-test/integration-failures.test.ts</c> holds the tool to, row for row: a failure's reading from a trx, vitest's
/// JSON or node's TAP, its signature and the key two runs compare by. Text only, no process.
/// </summary>
public sealed class FailureReadingTests
{
    private static JsonDocument Table() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "failure-signatures.json")));

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static TheoryData<int> Rows(string section)
    {
        var data = new TheoryData<int>();
        using var table = Table();
        for (var at = 0; at < table.RootElement.GetProperty(section).GetArrayLength(); at += 1) data.Add(at);
        return data;
    }

    public static TheoryData<int> NormaliseRows() => Rows("normalise");

    public static TheoryData<int> MethodRows() => Rows("methods");

    public static TheoryData<int> ExceptionRows() => Rows("exceptions");

    public static TheoryData<int> PathRows() => Rows("paths");

    public static TheoryData<int> ReportRows() => Rows("reports");

    public static TheoryData<int> SignatureRows() => Rows("signatures");

    public static TheoryData<int> GateRows() => Rows("gates");

    private static JsonElement Row(JsonDocument table, string section, int at) => table.RootElement.GetProperty(section)[at];

    [Theory]
    [MemberData(nameof(NormaliseRows))]
    public void A_line_is_normalised_as_the_shared_table_says(int at)
    {
        using var table = Table();
        var row = Row(table, "normalise", at);
        Assert.Equal((row.GetProperty("in").GetString(), row.GetProperty("out").GetString()), (row.GetProperty("in").GetString(), FailureReader.Normalise(row.GetProperty("in").GetString()!)));
    }

    [Theory]
    [MemberData(nameof(MethodRows))]
    public void A_dotnet_method_loses_its_compilers_wrapping_as_the_table_says(int at)
    {
        using var table = Table();
        var row = Row(table, "methods", at);
        Assert.Equal((row.GetProperty("in").GetString(), row.GetProperty("out").GetString()), (row.GetProperty("in").GetString(), FailureReader.DotnetMethod(row.GetProperty("in").GetString()!)));
    }

    [Fact]
    public void Frames_are_read_from_a_dotnet_stack_and_from_a_v8_one()
    {
        using var table = Table();
        var dotnet = table.RootElement.GetProperty("dotnetFrames");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(dotnet.GetProperty("frames").GetRawText()), FramesJson(FailureReader.DotnetFrames(dotnet.GetProperty("stack").GetString()!))));
        var v8 = table.RootElement.GetProperty("v8Frames");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(v8.GetProperty("frames").GetRawText()), FramesJson(FailureReader.V8Frames(v8.GetProperty("stack").GetString()!))));
    }

    private static JsonArray FramesJson(IReadOnlyList<FailureFrame> frames) => new([.. frames.Select(frame => frame.ToJson())]);

    [Theory]
    [MemberData(nameof(ExceptionRows))]
    public void An_exception_type_is_read_as_the_table_says(int at)
    {
        using var table = Table();
        var row = Row(table, "exceptions", at);
        Assert.Equal((Text(row, "message"), Text(row, "out")), (Text(row, "message"), FailureReader.ExceptionOf(Text(row, "message"), Text(row, "named"))));
    }

    [Theory]
    [MemberData(nameof(PathRows))]
    public void A_path_is_made_repository_relative_as_the_table_says(int at)
    {
        using var table = Table();
        var row = Row(table, "paths", at);
        Assert.Equal((Text(row, "file"), Text(row, "out")), (Text(row, "file"), FailureReader.RepositoryPath(Text(row, "file"), Text(row, "root"))));
    }

    [Theory]
    [MemberData(nameof(ReportRows))]
    public void A_reports_failures_are_read_by_its_kind_as_the_table_says(int at)
    {
        using var table = Table();
        var row = Row(table, "reports", at);
        var text = Text(row, "kind") == "vitest" ? row.GetProperty("json").GetRawText() : Text(row, "text")!;
        var read = FailureReader.ReadReport(text, null);
        var expected = JsonNode.Parse(row.GetProperty("failures").GetRawText());
        Assert.True(JsonNode.DeepEquals(expected, new JsonArray([.. read.Select(failure => failure.ToJson())])), Text(row, "name"));
    }

    [Fact]
    public void A_report_no_reader_knows_is_refused_in_the_tools_words()
    {
        var refused = Assert.Throws<FormatException>(() => FailureReader.ReadReport("nothing a reader knows"));
        Assert.Equal("not a trx, a vitest JSON report or a node --test TAP report", refused.Message);
    }

    private static ReadFailure FailureOf(JsonElement failure) => new(
        failure.GetProperty("runner").GetString()!, Text(failure, "unit"), failure.GetProperty("test").GetString()!, failure.GetProperty("exception").GetString()!,
        failure.GetProperty("message").GetString()!,
        [.. failure.GetProperty("frames").EnumerateArray().Select(frame => new FailureFrame(
            frame.GetProperty("method").GetString()!, Text(frame, "file"), frame.GetProperty("line").ValueKind == JsonValueKind.Number ? frame.GetProperty("line").GetInt32() : null))]);

    [Theory]
    [MemberData(nameof(SignatureRows))]
    public void A_signature_and_its_key_are_the_tables(int at)
    {
        using var table = Table();
        var row = Row(table, "signatures", at);
        var sources = table.RootElement.GetProperty("sources");
        var signature = FailureReader.SignatureOf(
            FailureOf(row.GetProperty("failure")), path => sources.TryGetProperty(path, out var source) ? source.GetString() : null, Text(row, "root"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("signature").GetRawText()), signature.ToJson()), Text(row, "name"));
        Assert.Equal((Text(row, "name"), Text(row, "key")), (Text(row, "name"), signature.Key));
    }

    [Theory]
    [MemberData(nameof(GateRows))]
    public void A_gate_that_names_no_test_is_signed_by_its_step_as_the_table_says(int at)
    {
        using var table = Table();
        var row = Row(table, "gates", at);
        var signature = FailureReader.GateSignature(Text(row, "gate")!, Text(row, "step")!, Text(row, "output")!, Text(row, "root"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("signature").GetRawText()), signature.ToJson()), Text(row, "name"));
        Assert.Equal((Text(row, "name"), Text(row, "key")), (Text(row, "name"), signature.Key));
    }

    [Fact]
    public void Two_signatures_are_one_failure_when_their_keys_are_one_and_a_builds_errors_are_a_set()
    {
        var one = FailureReader.GateSignature("driver", "build", "src/A/A.cs(1,1): error CS0103: a\nsrc/B/B.cs(2,2): error CS1002: b", null);
        var two = FailureReader.GateSignature("driver", "build", "src/B/B.cs(2,2): error CS1002: b\nsrc/A/A.cs(1,1): error CS0103: a", null);
        Assert.True(FailureReader.Same(one, two));
        Assert.False(FailureReader.Same(one, FailureReader.GateSignature("driver", "build", "src/A/A.cs(1,1): error CS0103: a", null)));
        Assert.False(FailureReader.Same(one, null));
        Assert.Equal("", FailureReader.KeyOf(null));
    }
}
