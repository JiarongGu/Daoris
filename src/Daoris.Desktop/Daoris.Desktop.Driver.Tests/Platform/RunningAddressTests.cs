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
