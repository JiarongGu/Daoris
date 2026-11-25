using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's running environments as its own <c>daoris.json</c> declares them: an id, the repository-relative paths
/// whose content is its version, the command that starts it, the ports it takes by name and the grace an unheld instance keeps.
/// </summary>
public sealed class EnvironmentDeclarationTests
{
    public const string Server = """
        {"source":"x","environments":[
          {"id":"game-server","inputs":["server/**","shared/proto/**"],"start":"dotnet run --project server","ports":["http","admin"],"graceSeconds":120}]}
        """;

    [Fact]
    public void An_environment_reads_with_its_inputs_start_command_ports_and_grace()
    {
        var read = EnvironmentDeclaration.Read(Server, "b1");

        Assert.Empty(read.Problems);
        Assert.Equal("b1", read.Blob);
        var server = Assert.Single(read.Items);
        Assert.Equal("game-server", server.Id);
        Assert.Equal(["server/**", "shared/proto/**"], server.Inputs);
        Assert.Equal("dotnet run --project server", server.Start);
        Assert.Equal(["http", "admin"], server.Ports);
        Assert.Equal(TimeSpan.FromSeconds(120), server.Grace);
    }

    [Fact]
    public void A_port_less_environment_with_no_grace_reads_with_the_default_grace()
    {
        var read = EnvironmentDeclaration.Read("""{"environments":[{"id":"db","inputs":["db/**"],"start":"run-db"}]}""");

        var db = Assert.Single(read.Items);
        Assert.Empty(db.Ports);
        Assert.Equal(EnvironmentDeclaration.DefaultGrace, db.Grace);
    }

    [Fact]
    public void A_manifest_that_declares_none_has_none_and_an_entry_that_does_not_read_is_left_out_naming_why()
    {
        Assert.Empty(EnvironmentDeclaration.Read("""{"source":"x"}""").Items);
        Assert.Empty(EnvironmentDeclaration.Read(null).Problems);

        var read = EnvironmentDeclaration.Read("""
            {"environments":[
              {"id":"ok","inputs":["a/**"],"start":"go"},
              {"id":"Bad","inputs":["a/**"],"start":"go"},
              {"id":"noinputs","start":"go"},
              {"id":"nostart","inputs":["a/**"]},
              {"id":"abs","inputs":["/etc/**"],"start":"go"},
              {"id":"ok","inputs":["b/**"],"start":"go"},
              {"id":"ports","inputs":["a/**"],"start":"go","ports":["Http"]},
              {"id":"grace","inputs":["a/**"],"start":"go","graceSeconds":-1},
              7]}
            """);

        Assert.Equal(["ok"], read.Items.Select(each => each.Id));
        Assert.Equal(8, read.Problems.Count);
        Assert.Contains(read.Problems, said => said.Contains("`Bad`", StringComparison.Ordinal) && said.Contains("lower-case", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`noinputs`", StringComparison.Ordinal) && said.Contains("inputs", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`nostart`", StringComparison.Ordinal) && said.Contains("start", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`abs`", StringComparison.Ordinal) && said.Contains("repository-relative", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("declared twice", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`ports`", StringComparison.Ordinal) && said.Contains("port name", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("`grace`", StringComparison.Ordinal) && said.Contains("graceSeconds", StringComparison.Ordinal));
        Assert.Contains(read.Problems, said => said.Contains("not an object", StringComparison.Ordinal));
    }

    [Fact]
    public void A_list_that_is_no_list_and_text_that_is_no_json_are_said_and_declare_none()
    {
        Assert.Contains("not a list", Assert.Single(EnvironmentDeclaration.Read("""{"environments":{}}""").Problems), StringComparison.Ordinal);
        Assert.Contains("not valid JSON", Assert.Single(EnvironmentDeclaration.Read("{ nope").Problems), StringComparison.Ordinal);
    }
}
