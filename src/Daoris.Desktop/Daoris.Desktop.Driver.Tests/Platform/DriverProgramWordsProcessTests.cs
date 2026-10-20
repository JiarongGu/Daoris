using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The seam's words arrive whole (PUSH1f2a, D194 point 1, the alone-path design §1.1): <c>daoris driver …</c>, started by Node through
/// the CLI's one spawn site, reaches a .NET program with the words it was typed with, read back by .NET's own reading, since a Node
/// stand-in would read them with Node's parser. The program here is a small native fixture this test builds, named as the driver's
/// apphost is; it prints what it read and exits as it is told. The terminal's environment arrives exactly, with no tools' <c>PATH</c>
/// laid over it, and the program's exit is the run's.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverProgramWordsProcessTests(DriverProgramWordsProcessTests.Echo echo) : IClassFixture<DriverProgramWordsProcessTests.Echo>
{
    private readonly string _root = echo.Root;

    /// <summary>The native fixture, built once for the class into a folder of its own under <c>_fixtures/</c>.</summary>
    public sealed class Echo : IDisposable
    {
        public string Root { get; } = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-program-words", Guid.NewGuid().ToString("N")[..8]);

        private readonly Lazy<string> _program;

        public Echo() => _program = new(() => Build(Root));

        public string Program => _program.Value;

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private const string Source =
        """
        using System.Text.Json;
        var exit = Environment.GetEnvironmentVariable("ECHO_EXIT") is { Length: > 0 } code ? int.Parse(code) : 0;
        Console.Out.Write(JsonSerializer.Serialize(new
        {
            args,
            path = Environment.GetEnvironmentVariable("PATH"),
            session = Environment.GetEnvironmentVariable("DAORIS_SESSION_ID"),
        }));
        return exit;
        """;

    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <AssemblyName>daoris-driver</AssemblyName>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>The fixture's apphost, built into <paramref name="root"/>.</summary>
    private static string Build(string root)
    {
        var source = Path.Combine(root, "fixture");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Echo.csproj"), Project);
        File.WriteAllText(Path.Combine(source, "Program.cs"), Source);
        // Nothing above this folder is the fixture's to read.
        File.WriteAllText(Path.Combine(source, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(source, "Directory.Packages.props"), "<Project />");
        var output = Path.Combine(root, "built");
        var (code, said, _) = Run("dotnet", ["build", Path.Combine(source, "Echo.csproj"), "-nodeReuse:false", "-v", "q", "-o", output], null);
        Assert.True(code == 0, said);
        var program = Path.Combine(output, OperatingSystem.IsWindows() ? "daoris-driver.exe" : "daoris-driver");
        Assert.True(File.Exists(program), $"the fixture built no apphost: {said}");
        return program;
    }

    private static (int Code, string Out, string Error) Run(string file, IEnumerable<string> words, IDictionary<string, string?>? environment)
    {
        var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var word in words) info.ArgumentList.Add(word);
        if (environment is not null)
        {
            info.Environment.Clear();
            foreach (var (name, value) in environment) info.Environment[name] = value;
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(3)), $"{file} did not end");
        return (process.ExitCode, output.Result, error.Result);
    }

    /// <summary>The terminal a person types <c>daoris driver …</c> in: this machine's own variables, the program named, and a home inside an install whose launchers' folder the tools' PATH would put first.</summary>
    private Dictionary<string, string?> Terminal(string program, string? exit = null)
    {
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(Path.Combine(install, "app", "bin"));
        Directory.CreateDirectory(Path.Combine(install, "data"));
        File.WriteAllText(Path.Combine(install, Daoris.Driver.DaorisHome.InstallMarker), "");
        var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
        environment["DAORIS_DRIVER_PROGRAM"] = program;
        environment["DAORIS_HOME"] = Path.Combine(install, "data");
        environment["DAORIS_SESSION_ID"] = "s-words";
        environment["ECHO_EXIT"] = exit;
        return environment;
    }

    private static string Cli => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "bin", "daoris.mjs");

    [Fact]
    public void The_words_arrive_whole_and_the_terminals_environment_exactly()
    {
        var program = echo.Program;
        string[] words =
        [
            "standing", "engine", "a \"quoted\" word", "this & that", "%PATH% stays", @"C:\a path\""x", @"C:\Program Files\x", @"ends\",
            "中文的话", "", "--clear",
        ];
        var terminal = Terminal(program);

        var (code, output, error) = Run("node", [Cli, "driver", .. words], terminal);

        Assert.True(code == 0, $"{output}\n{error}");
        using var said = JsonDocument.Parse(output);
        Assert.Equal(["driver", .. words], said.RootElement.GetProperty("args").EnumerateArray().Select(each => each.GetString()!));
        // Exactly: the launchers' folder beside the home is not laid first, as every other child the CLI starts has it.
        Assert.Equal(terminal["PATH"], said.RootElement.GetProperty("path").GetString());
        Assert.Equal("s-words", said.RootElement.GetProperty("session").GetString());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("7", 2)]
    public void The_programs_own_answers_are_the_runs_and_any_other_is_2_naming_it(string exit, int expected)
    {
        var program = echo.Program;

        var (code, output, error) = Run("node", [Cli, "driver", "list"], Terminal(program, exit));

        Assert.True(code == expected, $"{output}\n{error}");
        if (exit == "7") Assert.Contains("ended with status 7", output + error, StringComparison.Ordinal);
    }
}
