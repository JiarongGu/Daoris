using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The address the running desktop leaves in its home (FINDDAORIS1, D171 point 1): the service it holds or adopted, and
/// the process that holds it, written whole, and removed by the process that wrote it, only while it still says what that
/// process wrote.
/// </summary>
public sealed class RunningAddressTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-running-tests", Guid.NewGuid().ToString("N")[..8]);

    public RunningAddressTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void It_names_the_address_and_the_process_that_holds_it()
    {
        using var written = RunningAddress.Write(_home, "http://localhost:5177/");

        Assert.NotNull(written);
        var named = RunningAddress.Read(_home);
        Assert.Equal("http://localhost:5177", named!.Url);
        Assert.Equal(Environment.ProcessId, named.Pid);
        using var self = Process.GetCurrentProcess();
        Assert.Equal(self.StartTime.ToUniversalTime().Ticks, named.Started);

        // HOMEID1: and the home's id, minted where it had none, so a reader tells this home's address from a copy's.
        Assert.True(HomeIdentity.IsId(named.Home), named.Home);
        Assert.Equal(HomeIdentity.Read(_home), named.Home);

        // One JSON object, BOM-less, beside nothing half-written.
        var text = File.ReadAllText(RunningAddress.PathIn(_home));
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(text).RootElement.ValueKind);
        Assert.Equal(
            [HomeIdentity.FileName, Path.GetFileName(RunningAddress.PathIn(_home))],
            Directory.GetFiles(_home).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>A file from before HOMEID1 names no home, and still reads: fields are added, never changed in place.</summary>
    [Fact]
    public void A_file_from_before_names_no_home_and_still_reads()
    {
        File.WriteAllText(RunningAddress.PathIn(_home), "{\"url\":\"http://localhost:5177\",\"pid\":1,\"started\":1,\"since\":\"2026-10-16T08:00:00+00:00\"}");

        var named = RunningAddress.Read(_home);

        Assert.Equal("http://localhost:5177", named!.Url);
        Assert.Null(named.Home);
        Assert.Null(named.Pipe);
    }

    /// <summary>
    /// RPC1a (the terminal RPC design §4.1, §6.3): once the instance serves its pipe, the file names it beside the rest, so a
    /// client that reads one finds it served. Its name is the home's id and the Windows session's.
    /// </summary>
    [Fact]
    public void It_names_the_pipe_its_instance_serves()
    {
        var id = HomeIdentity.Ensure(_home)!;
        using var written = RunningAddress.Write(_home, "http://localhost:5177", PipeName.For(id, 3));

        var named = RunningAddress.Read(_home);

        Assert.Equal($"daoris-{id}-3", named!.Pipe);
        Assert.Equal(id, named.Home);
        Assert.Equal("http://localhost:5177", named.Url);
    }

    /// <summary>
    /// The pipe's rows, held case for case by the CLI's twin (`running.test.ts`): a pipe is read only where its name is the
    /// pipe of the home the file names, so a file naming another home's pipe, or something that is no pipe's name, names
    /// none, and the rest of it still reads.
    /// </summary>
    [Theory]
    [InlineData("\"daoris-0123456789abcdef0123456789abcdef-1\"", "daoris-0123456789abcdef0123456789abcdef-1")]
    [InlineData("\"daoris-fedcba9876543210fedcba9876543210-1\"", null)]
    [InlineData("\"daoris-0123456789abcdef0123456789abcdef-\"", null)]
    [InlineData("\"daoris-0123456789abcdef0123456789abcdef-1x\"", null)]
    [InlineData("\"\\\\\\\\.\\\\pipe\\\\daoris-0123456789abcdef0123456789abcdef-1\"", null)]
    [InlineData("\"\"", null)]
    [InlineData("7", null)]
    [InlineData("null", null)]
    public void A_pipe_is_read_only_where_it_is_the_named_home_s(string pipe, string? read)
    {
        File.WriteAllText(RunningAddress.PathIn(_home),
            "{\"url\":\"http://localhost:5177\",\"pid\":1,\"started\":1,\"since\":\"2026-10-16T08:00:00+00:00\","
            + $"\"home\":\"0123456789abcdef0123456789abcdef\",\"pipe\":{pipe}}}");

        var named = RunningAddress.Read(_home);

        Assert.Equal("http://localhost:5177", named!.Url);
        Assert.Equal(read, named.Pipe);
    }

    /// <summary>A file naming no home names no pipe either: a pipe's name is checked against the home it is said to be.</summary>
    [Fact]
    public void A_file_naming_no_home_names_no_pipe()
    {
        File.WriteAllText(RunningAddress.PathIn(_home),
            "{\"url\":\"http://localhost:5177\",\"pid\":1,\"started\":1,\"pipe\":\"daoris-0123456789abcdef0123456789abcdef-1\"}");

        Assert.Null(RunningAddress.Read(_home)!.Pipe);
    }

    [Fact]
    public void Its_writer_removes_it_and_nothing_another_wrote_since()
    {
        var first = RunningAddress.Write(_home, "http://localhost:5177");
        first!.Dispose();
        Assert.False(File.Exists(RunningAddress.PathIn(_home)));

        // Another Daoris on the same home wrote its own since: the first one's leaving leaves it.
        var stale = RunningAddress.Write(_home, "http://localhost:5177");
        File.WriteAllText(RunningAddress.PathIn(_home), new JsonObject
        {
            ["url"] = "http://localhost:5199", ["pid"] = Environment.ProcessId, ["started"] = 1L, ["since"] = "2026-10-16T08:00:00+00:00",
        }.ToJsonString());
        stale!.Dispose();
        Assert.Equal("http://localhost:5199", RunningAddress.Read(_home)!.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"url\":\"http://localhost:5177\"}")]
    [InlineData("{\"url\":\"\",\"pid\":1,\"started\":1}")]
    public void A_file_that_does_not_read_names_nothing(string text)
    {
        File.WriteAllText(RunningAddress.PathIn(_home), text);
        Assert.Null(RunningAddress.Read(_home));
    }

    [Fact]
    public void A_home_with_none_names_nothing() => Assert.Null(RunningAddress.Read(_home));
}
