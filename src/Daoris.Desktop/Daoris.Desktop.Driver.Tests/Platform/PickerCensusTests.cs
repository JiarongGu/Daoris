using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every system picker the desktop opens is the kit's (<c>IFileDialogs</c>), every window thread's exception reaches the machine log
/// through the kit's sink, and the launcher keeps the kit's relaxed context validation. A source scan, as
/// <see cref="ProcessStartsCensusTests"/>: the next picker written here will not know to use the kit's.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class PickerCensusTests
{
    private static readonly string[] Projects =
        ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App", "Daoris.Desktop.Launcher"];

    private static readonly Regex Picker = new(@"\b(OpenFileDialog|SaveFileDialog|FolderBrowserDialog|IFileOpenDialog|IFileSaveDialog|IFileDialog|GetOpenFileName|GetSaveFileName|SHBrowseForFolder)\b");

    [Fact]
    public void No_source_shows_a_system_picker_of_its_own()
    {
        var found = Projects
            .SelectMany(project => SourceFiles.All(project, "*.cs"))
            .Where(path => Picker.IsMatch(Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "")))
            .Select(Path.GetFileName)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void Every_picker_is_the_kits_dialog_through_the_two_doors_the_page_has()
    {
        var main = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Program.cs"));

        Assert.Equal(1, Regex.Matches(main, @"\.OpenFileAsync\(").Count);
        Assert.Equal(1, Regex.Matches(main, @"\.OpenFolderAsync\(").Count);
    }

    [Fact]
    public void Every_window_thread_is_a_single_threaded_apartment_and_its_exceptions_reach_the_log_through_the_kits_sink()
    {
        var main = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Program.cs"));
        Assert.Matches(@"\[STAThread\]\s+private static void Main", main);
        Assert.Contains("OnUnhandledException = report =>", main);
        Assert.Contains("ShowCrashDialog = false", main);
        Assert.Contains("UnhandledExceptionSource.UiThread", main);
        Assert.DoesNotContain("Application.ThreadException", main);
    }

    [Fact]
    public void The_launcher_keeps_the_kits_relaxed_context_validation()
    {
        var project = File.ReadAllText(Path.Combine(SourceFiles.Project("Daoris.Desktop.App"), "Daoris.Desktop.App.csproj"));

        Assert.DoesNotContain("ShenoraChromiumRelaxContextIp", project);
        Assert.DoesNotContain("cet-relax", project);
        Assert.DoesNotContain("<Exec", project);
    }
}
