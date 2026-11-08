using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every system file or folder picker the desktop opens is shown by <c>Pickers</c>, on its owner window's own thread (a
/// single-threaded apartment with its pump, which every window's thread is), and a process that shows one survives what a
/// shell extension does under its right-click menu. A source scan, as <see cref="ProcessStartsCensusTests"/>: the next
/// picker written elsewhere will not know to.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class PickerCensusTests
{
    private static readonly string[] Projects =
        ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App", "Daoris.Desktop.Launcher"];

    private static readonly Regex Picker = new(@"\b(OpenFileDialog|SaveFileDialog|FolderBrowserDialog|IFileOpenDialog|IFileSaveDialog|IFileDialog|GetOpenFileName|GetSaveFileName|SHBrowseForFolder)\b");

    [Fact]
    public void Every_picker_is_shown_by_Pickers()
    {
        var found = Projects
            .SelectMany(project => SourceFiles.All(project, "*.cs").Select(path => (Project: project, Path: path)))
            .Where(each => Picker.IsMatch(Regex.Replace(File.ReadAllText(each.Path), @"//[^\n]*", "")))
            .Select(each => Path.GetFileName(each.Path))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["Pickers.cs"], found);
    }

    [Fact]
    public void Pickers_show_a_dialog_only_on_the_owner_window_s_thread()
    {
        var source = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Pickers.cs"));

        Assert.Equal(2, Regex.Matches(source, @"\bShowDialog\(owner\)").Count);
        Assert.Equal(2, Regex.Matches(source, @"if \(owner\.InvokeRequired\) return \(string\?\)owner\.Invoke\(").Count);
        Assert.DoesNotContain("ShowDialog()", source);
        Assert.DoesNotContain("Task.Run", source);
        Assert.DoesNotContain("new Thread", source);
    }

    [Fact]
    public void Every_window_thread_is_a_single_threaded_apartment_and_its_exceptions_are_caught()
    {
        var main = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Program.cs"));
        Assert.Matches(@"\[STAThread\]\s+private static void Main", main);
        Assert.Contains("UiExceptions.Watch(log);", main);

        var watch = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "UiExceptions.cs"));
        Assert.Contains("SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)", watch);
        Assert.Contains("Application.ThreadException +=", watch);
    }

    [Fact]
    public void The_launcher_is_laid_out_with_its_context_validation_relaxed_by_a_build_and_a_publish()
    {
        var project = File.ReadAllText(Path.Combine(SourceFiles.Project("Daoris.Desktop.App"), "Daoris.Desktop.App.csproj"));

        Assert.Equal(2, Regex.Matches(project, @"tools\\cet-relax\.mjs").Count);
        Assert.Contains("AfterTargets=\"ShenoraChromiumLayout\"", project);
        Assert.Contains("AfterTargets=\"ShenoraChromiumPublishLayout\"", project);
    }
}
