using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The release of a kept build base stands on MSBuild's own account of a build: the <c>FileWrites</c> and <c>FileWritesShareable</c>
/// items <c>-getItem</c> reports once the targets ran, which its incremental clean trusts with deletion. These hold, over a real
/// <c>dotnet build</c> of a program that references a library, a package and a copied content file, that the account is reported
/// after the build, that it names every file under the configuration's output folder, and that a second build with nothing to do
/// reports the same whole list; a file the build did not write is never named.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class BuildAccountProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "build-account-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_build_reports_every_file_of_its_output_folder_and_a_build_with_nothing_to_do_reports_the_same()
    {
        var app = Seed();
        var output = Path.Combine(app, "bin", "Debug");

        var first = Account(app);
        var onDisk = FilesUnder(output);
        Assert.Contains(Path.Combine(output, "net10.0", "App.dll"), onDisk);
        Assert.Contains(Path.Combine(output, "net10.0", "Lib.dll"), onDisk);
        Assert.Contains(Path.Combine(output, "net10.0", "System.Security.Cryptography.ProtectedData.dll"), onDisk);
        Assert.Contains(Path.Combine(output, "net10.0", "settings.json"), onDisk);
        Assert.Equal(onDisk, Under(first, output));

        var planted = Path.Combine(output, "net10.0", "Old.dll");
        File.WriteAllText(planted, "another build's");
        var second = Account(app);

        Assert.Equal(Under(first, output), Under(second, output));
        Assert.DoesNotContain(planted, second);
        Assert.Equal(onDisk.Append(planted).Order(StringComparer.OrdinalIgnoreCase), FilesUnder(output));
    }

    /// <summary>A program with a project reference, a package reference and a content file copied beside it, reading nothing above its folder.</summary>
    private string Seed()
    {
        var lib = Path.Combine(_root, "Lib");
        var app = Path.Combine(_root, "App");
        Directory.CreateDirectory(lib);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"), "<Project />");
        File.WriteAllText(Path.Combine(lib, "Lib.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(lib, "Greeting.cs"), "namespace Lib; public static class Greeting { public static string Text => \"hi\"; }");
        File.WriteAllText(Path.Combine(app, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Lib\Lib.csproj" />
                <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.14" />
                <None Include="settings.json" CopyToOutputDirectory="PreserveNewest" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(app, "Program.cs"), "System.Console.WriteLine(Lib.Greeting.Text);");
        File.WriteAllText(Path.Combine(app, "settings.json"), "{}");
        return app;
    }

    /// <summary>The build's own account: the full paths of its <c>FileWrites</c> and <c>FileWritesShareable</c> items, read from the run that built.</summary>
    private static string[] Account(string folder)
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var word in new[] { "build", "App.csproj", "-c", "Debug", "-nodeReuse:false", "--nologo", "-t:Build", "-getItem:FileWrites", "-getItem:FileWritesShareable" })
            info.ArgumentList.Add(word);
        using var process = Process.Start(info)!;
        var said = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(4)), "the build did not end");
        Assert.True(process.ExitCode == 0, said.Result + error.Result);
        using var json = JsonDocument.Parse(said.Result);
        var items = json.RootElement.GetProperty("Items");
        return new[] { "FileWrites", "FileWritesShareable" }
            .Where(name => items.TryGetProperty(name, out _))
            .SelectMany(name => items.GetProperty(name).EnumerateArray())
            .Select(item => Path.GetFullPath(item.GetProperty("FullPath").GetString()!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] Under(IEnumerable<string> account, string folder) =>
        account.Where(path => path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string[] FilesUnder(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(Path.GetFullPath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
}
