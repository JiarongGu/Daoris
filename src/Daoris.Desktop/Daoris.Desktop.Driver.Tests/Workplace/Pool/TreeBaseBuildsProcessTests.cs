using System.Diagnostics;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The door over a real <c>dotnet build</c>: a base whose folder holds a replaced project's leftovers, a <c>publish/</c> and a runtime's
/// folder beside its producer's build is released holding exactly what that build owns, the rest set aside with the previous line's and
/// the marker gone; a restore and a failed build release nothing; a path with no base says so and removes a marker the record no longer names.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class TreeBaseBuildsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "tree-base-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Tree => Path.Combine(_root, "engine");

    private string Aside => Path.Combine(_root, "aside", "s-aaaa0001-1");

    private string Output => Path.Combine(Tree, "App", "bin", "Debug", "net10.0");

    public void Dispose() => GitSeed.Discard(_root);

    [Fact]
    public async Task The_doors_build_releases_exactly_what_its_producer_owns_and_sets_the_rest_aside_with_the_previous_lines()
    {
        await SeedAsync();
        foreach (var leftover in new[] { "Old.exe", "Old.dll", "Old.deps.json", Path.Combine("publish", "App.dll"), Path.Combine("linux-x64", "App.dll") })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Output, leftover))!);
            await File.WriteAllTextAsync(Path.Combine(Output, leftover), "s-aaaa0001's\n");
        }

        var said = new StringWriter();
        var exit = await TreeBaseBuilds.BuildAsync(Home, Path.Combine(Tree, "App"), said, said);

        Assert.True(exit == 0, said.ToString());
        Assert.Contains("bin/Debug/ released, built by App/App.csproj -c Debug", said.ToString());
        Assert.Contains("5 entries its build does not own set aside with s-aaaa0001's", said.ToString());
        Assert.Empty(TreeFilesRecord.Read(Home, Tree)!.Bases);
        Assert.Null(BaseMarker.Above(Path.Combine(Output, "App.dll")));
        Assert.True(File.Exists(Path.Combine(Output, "App.dll")));
        Assert.False(File.Exists(Path.Combine(Output, "Old.exe")));
        Assert.False(Directory.Exists(Path.Combine(Output, "publish")));
        Assert.True(File.Exists(Path.Combine(Aside, "App", "bin", "Debug", "net10.0", "Old.exe")));
        Assert.True(File.Exists(Path.Combine(Aside, "App", "bin", "Debug", "net10.0", "linux-x64", "App.dll")));
        Assert.Equal("s-aaaa0001", PoolAsides.LineOf(Aside));
    }

    [Fact]
    public async Task A_restore_and_a_failed_build_release_nothing_and_the_marker_stands()
    {
        await SeedAsync();
        Assert.Equal(0, Run(Path.Combine(Tree, "App"), "restore", "-nodeReuse:false"));
        Assert.NotNull(BaseMarker.Above(Path.Combine(Output, "App.dll")));
        await File.WriteAllTextAsync(Path.Combine(Tree, "App", "Program.cs"), "this does not compile");

        var said = new StringWriter();
        var exit = await TreeBaseBuilds.BuildAsync(Home, Path.Combine(Output, "App.dll"), said, said);

        Assert.Equal(1, exit);
        Assert.Contains("so nothing is released", said.ToString());
        Assert.Single(TreeFilesRecord.Read(Home, Tree)!.Bases);
        Assert.Equal("s-aaaa0001", BaseMarker.Above(Path.Combine(Output, "App.dll"))?.From);
    }

    [Fact]
    public async Task A_path_with_no_base_releases_nothing_and_a_marker_the_record_no_longer_names_is_removed()
    {
        await SeedAsync();
        TreeFilesRecord.Change(Home, Tree, state => state with { Bases = [] });

        var said = new StringWriter();
        var exit = await TreeBaseBuilds.BuildAsync(Home, Path.Combine(Tree, "App", "bin", "Debug"), said, said);

        Assert.Equal(0, exit);
        Assert.Contains("named a base the record no longer holds", said.ToString());
        Assert.Null(BaseMarker.Above(Path.Combine(Output, "App.dll")));
    }

    /// <summary>A checkout whose program the previous line built, its <c>bin/Debug/</c> kept as that line's base: recorded and marked.</summary>
    private async Task SeedAsync()
    {
        var app = Path.Combine(Tree, "App");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(Home);
        foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" }) await File.WriteAllTextAsync(Path.Combine(Tree, name), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(app, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(app, "Program.cs"), "System.Console.WriteLine(\"hi\");");
        await File.WriteAllTextAsync(Path.Combine(Tree, ".gitignore"), "bin/\nobj/\n");
        await GitAsync(Tree, "init", "--quiet", "-b", "main");
        await GitAsync(Tree, "add", ".");
        await GitAsync(Tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "first");
        Assert.Equal(0, Run(app, "build", "-nodeReuse:false", "--nologo", "-v", "q"));

        var kept = new TreeBase("App/bin/Debug/", "s-aaaa0001", "0000000", DateTimeOffset.UtcNow, "App/App.csproj", "Debug") { Aside = Aside };
        TreeBases.Write(Home, Tree, [kept]);
    }

    private static int Run(string folder, params string[] words)
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var word in words) info.ArgumentList.Add(word);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(4)), "dotnet did not end");
        Assert.True(process.ExitCode is 0 or 1, output.Result + error.Result);
        return process.ExitCode;
    }
}
