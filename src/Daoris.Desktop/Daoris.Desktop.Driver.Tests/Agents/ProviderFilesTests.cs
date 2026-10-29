using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 3 (the agent-provider design §11 row b; D213's row b part 3 note): each provider's <c>provider.json</c> is the
/// one place a tool reads its stand-in facts, so it is held to <c>Providers.Built()</c> field by field and the C# list and the files cannot
/// drift. A stand-in's program sits in its folder by one layout, <c>Agents/Providers/&lt;Folder&gt;/Program/**</c>, and the driver project's
/// one glob carries every file of it beside the driver, so this test's own build output holds it.
/// </summary>
public sealed class ProviderFilesTests
{
    /// <summary>Every <c>provider.json</c> keeps the layout and states what the registration list registers: the id, the flag, each door and its kind.</summary>
    [Fact]
    public void Every_provider_file_agrees_with_the_registration_list_field_by_field()
    {
        var read = ProviderFiles.All();
        Assert.NotEmpty(read);
        Assert.Empty(read.SelectMany(each => each.Problems));

        foreach (var file in read.Select(each => each.File!))
        {
            var provider = Assert.Single(Providers.Built(), each => Ids.Same(each.Id.Value, file.Id));
            Assert.Equal(provider.Identity.StandIn, file.StandIn);
            Assert.Equal(
                provider.Doors.Select(door => (door.Id.Value, KindOf(door.Transport))).Order(),
                file.Doors.Select(door => (door.Id, door.Kind)).Order());
        }
    }

    /// <summary>Every provider the registration list registers brings its file, so the tools find each stand-in by the files alone.</summary>
    [Fact]
    public void Every_registered_provider_brings_its_file()
    {
        Assert.Equal(
            Providers.Built().Select(provider => provider.Id.Value).Order(StringComparer.OrdinalIgnoreCase),
            ProviderFiles.All().Select(each => each.File!.Id).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(Providers.Built().Where(provider => provider.Identity.StandIn), provider => Assert.True(ProviderFiles.Of(provider.Id.Value).StandIn));
    }

    /// <summary>
    /// A stand-in's program is its folder's <c>Program/</c> entry, each door chosen by its own arguments, and the project's glob carries
    /// every file under a provider's <c>Program/</c> into the build output beside the driver, byte for byte, none listed by hand.
    /// </summary>
    [Fact]
    public void A_stand_in_s_program_sits_in_its_folder_and_the_glob_carries_it_beside_the_driver()
    {
        var standIns = ProviderFiles.All().Select(each => each.File!).Where(file => file.StandIn).ToList();
        Assert.NotEmpty(standIns);

        foreach (var file in standIns)
        {
            Assert.True(File.Exists(Path.Combine(file.Folder, ProviderFiles.ProgramFolder, file.Entry!)), $"{file.Id}: no {file.Entry} under its {ProviderFiles.ProgramFolder}/");
            Assert.True(File.Exists(file.Built(AppContext.BaseDirectory)), $"{file.Id}: the build output beside the driver holds no {file.Entry}");
        }

        var carried = Directory.EnumerateDirectories(ProviderFiles.ProvidersFolder)
            .Select(folder => Path.Combine(folder, ProviderFiles.ProgramFolder))
            .Where(Directory.Exists)
            .SelectMany(program => Directory.EnumerateFiles(program, "*", SearchOption.AllDirectories))
            .ToList();
        Assert.NotEmpty(carried);
        foreach (var source in carried)
        {
            var built = Path.Combine(AppContext.BaseDirectory, "Agents", "Providers", Path.GetRelativePath(ProviderFiles.ProvidersFolder, source));
            Assert.True(File.Exists(built), $"{ConformancePending.Relative(source)} is not carried beside the driver");
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(built));
        }
    }

    /// <summary>The reader refuses each break of the layout, naming the field, so a file a tool reads is never half right.</summary>
    [Theory]
    [InlineData("""{ "id": "x", "standIn": true, "program": { "entry": "a.mjs" }, "doors": [ { "id": "x", "kind": "native", "arguments": ["n"] } ], "extra": 1 }""", "'extra' is no field")]
    [InlineData("""{ "standIn": false, "doors": [ { "id": "x", "kind": "native" } ] }""", "'id' is missing")]
    [InlineData("""{ "id": "x", "standIn": false, "doors": [ { "id": "x", "kind": "pipe" } ] }""", "'kind' is one of native, protocol")]
    [InlineData("""{ "id": "x", "standIn": true, "doors": [ { "id": "x", "kind": "native", "arguments": ["n"] } ] }""", "a stand-in states its 'program'")]
    [InlineData("""{ "id": "x", "standIn": true, "program": { "entry": "a.mjs" }, "doors": [ { "id": "x", "kind": "native" } ] }""", "states the 'arguments' that choose it")]
    [InlineData("""{ "id": "x", "standIn": true, "program": { "entry": "a.mjs" }, "doors": [ { "id": "x", "kind": "native", "arguments": ["n"] }, { "id": "y", "kind": "protocol", "arguments": ["n"] } ] }""", "two doors state the same arguments")]
    [InlineData("""{ "id": "x", "standIn": true, "program": { "entry": "../a.mjs" }, "doors": [ { "id": "x", "kind": "native", "arguments": ["n"] } ] }""", "never outside it")]
    [InlineData("""{ "id": "x", "standIn": false, "doors": [] }""", "'doors' is an array")]
    public void The_reader_refuses_a_file_out_of_the_layout(string text, string says)
    {
        var read = ProviderFiles.Parse(text, Path.Combine(ProviderFiles.ProvidersFolder, "Planted", ProviderFiles.FileName));

        Assert.Null(read.File);
        Assert.Contains(read.Problems, problem => problem.Contains(says, StringComparison.Ordinal));
    }

    private static string KindOf(Transport transport) => transport switch
    {
        Transport.Native => "native",
        Transport.Protocol => "protocol",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, "a transport the files have no kind for"),
    };
}
