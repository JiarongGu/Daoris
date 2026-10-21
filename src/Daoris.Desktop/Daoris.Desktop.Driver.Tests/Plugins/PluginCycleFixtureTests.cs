using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1e: the fixture `plugins try --cycle` reads. Each field is optional and its default stands where it is absent; a field
/// of the wrong shape is refused naming it, before anything starts.
/// </summary>
public sealed class PluginCycleFixtureTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-cycle-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginCycleFixtureTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private string Write(string text)
    {
        var file = Path.Combine(_scratch, Guid.NewGuid().ToString("N")[..6] + ".json");
        File.WriteAllText(file, text);
        return file;
    }

    [Fact]
    public void The_fixture_the_kit_writes_reads_as_the_defaults_with_its_step()
    {
        var read = PluginCycles.Read(Write(PluginCycles.Fixture("deploy")));

        Assert.Equal("deploy", read.Step);
        Assert.Equal(CycleFixture.Default.Unit, read.Unit);
        Assert.Equal(CycleFixture.Default.Revisions, read.Revisions);
        Assert.Empty(read.Inputs);
        Assert.Equal(CycleFixture.Default.Deadline, read.Deadline);
    }

    [Fact]
    public void An_empty_object_is_every_default_and_a_field_it_does_not_know_is_passed_over()
    {
        var read = PluginCycles.Read(Write("""{ "note": "anything" }"""));

        Assert.Equal(CycleFixture.Default, read);
    }

    [Fact]
    public void Each_field_is_read()
    {
        var read = PluginCycles.Read(Write("""
            { "step": "ship", "unit": "u-9", "revisions": ["a", "b"], "inputs": ["acme.build/artifact@1:x"], "deadlineSeconds": 7 }
            """));

        Assert.Equal(("ship", "u-9", "b", "acme.build/artifact@1:x", TimeSpan.FromSeconds(7)),
            (read.Step, read.Unit, read.Revisions[1], read.Inputs.Single(), read.Deadline));
    }

    [Theory]
    [InlineData("""{ "step": "Ship" }""", "`step`")]
    [InlineData("""{ "unit": "" }""", "`unit`")]
    [InlineData("""{ "revisions": ["a"] }""", "`revisions`")]
    [InlineData("""{ "revisions": ["a", "a"] }""", "`revisions`")]
    [InlineData("""{ "inputs": "x" }""", "`inputs`")]
    [InlineData("""{ "deadlineSeconds": 0 }""", "`deadlineSeconds`")]
    [InlineData("""{ "deadlineSeconds": 61 }""", "`deadlineSeconds`")]
    [InlineData("""[1]""", "is not a JSON object")]
    [InlineData("""not json""", "is not a JSON object")]
    public void A_field_of_the_wrong_shape_is_refused_naming_it(string text, string named)
    {
        var refused = Assert.Throws<DriverException>(() => PluginCycles.Read(Write(text)));

        Assert.Contains(named, refused.Message);
    }

    [Fact]
    public void A_fixture_that_is_not_there_is_refused()
    {
        var refused = Assert.Throws<DriverException>(() => PluginCycles.Read(Path.Combine(_scratch, "none.json")));

        Assert.Contains("no file", refused.Message);
    }
}
