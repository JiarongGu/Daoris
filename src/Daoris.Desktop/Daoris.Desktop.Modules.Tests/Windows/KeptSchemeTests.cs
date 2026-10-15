using System.Text;
using Daoris.Driver;
using Shenora.Core.Shell;
using Shenora.Modules.Platform;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// THEMESTART1: the window's first frame wears the viewer's saved choice, not the OS's. The kit keeps the app's colour
/// scheme in memory only and asks the app to pass the saved one at its start (<c>IColorScheme</c>'s remarks), so the
/// shell keeps each choice the page sets, reads it back before the window is made, and starts the window's palette and
/// the engine from it. Before this the first frame followed the OS and flipped once the page spoke (D22's THEMESCHEME1
/// note).
/// </summary>
public sealed class KeptSchemeTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-themestart1-" + Guid.NewGuid().ToString("N")[..8]);

    public KeptSchemeTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Kept => Path.Combine(_folder, KeptScheme.FileName);

    /// <summary>A first start, with nothing kept: the app follows the OS, as it always did.</summary>
    [Fact]
    public void Nothing_kept_starts_at_System()
    {
        Assert.Equal(ColorScheme.System, KeptScheme.Read(Kept));
    }

    /// <summary>Each choice the page sets is the one the next start reads, whatever it was started at.</summary>
    [Theory]
    [InlineData(ColorScheme.Light)]
    [InlineData(ColorScheme.Dark)]
    [InlineData(ColorScheme.System)]
    public void Each_choice_set_is_the_one_the_next_start_reads(ColorScheme choice)
    {
        var scheme = new ColorSchemeState(choice == ColorScheme.Dark ? ColorScheme.Light : ColorScheme.Dark);
        using var kept = KeptScheme.Keep(scheme, Kept, MachineLog.None);

        scheme.Set(choice);

        Assert.Equal(choice, KeptScheme.Read(Kept));
    }

    /// <summary>The latest of several, and nothing more once the keeping ends.</summary>
    [Fact]
    public void The_latest_choice_is_kept_and_none_after_the_keeping_ends()
    {
        var scheme = new ColorSchemeState(ColorScheme.System);
        var kept = KeptScheme.Keep(scheme, Kept, MachineLog.None);

        scheme.Set(ColorScheme.Dark);
        scheme.Set(ColorScheme.Light);
        Assert.Equal(ColorScheme.Light, KeptScheme.Read(Kept));

        kept.Dispose();
        scheme.Set(ColorScheme.Dark);
        Assert.Equal(ColorScheme.Light, KeptScheme.Read(Kept));
    }

    /// <summary>
    /// What is kept is a word a person can read, BOM-less UTF-8 with LF, as every file the shell writes; an unknown
    /// word or an unreadable file is a start at <c>System</c>, never a window that does not open.
    /// </summary>
    [Fact]
    public void The_file_is_a_word_and_anything_else_starts_at_System()
    {
        var scheme = new ColorSchemeState(ColorScheme.System);
        using (KeptScheme.Keep(scheme, Kept, MachineLog.None)) scheme.Set(ColorScheme.Dark);

        var bytes = File.ReadAllBytes(Kept);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "a BOM was written");
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Equal("{\"scheme\":\"dark\"}\n", text);

        foreach (var unread in new[] { "", "not json", "{\"scheme\":\"sepia\"}", "{\"scheme\":3}", "[]" })
        {
            File.WriteAllText(Kept, unread);
            Assert.Equal(ColorScheme.System, KeptScheme.Read(Kept));
        }
    }

    /// <summary>
    /// The first frame's palette: a held choice whatever the OS says, and the OS's at <c>System</c>, asked only then.
    /// </summary>
    [Theory]
    [InlineData(ColorScheme.Light, true, false)]
    [InlineData(ColorScheme.Light, false, false)]
    [InlineData(ColorScheme.Dark, false, true)]
    [InlineData(ColorScheme.Dark, true, true)]
    [InlineData(ColorScheme.System, true, true)]
    [InlineData(ColorScheme.System, false, false)]
    public void The_first_frame_wears_a_held_choice_and_the_OS_at_System(ColorScheme scheme, bool osDark, bool dark)
    {
        var asked = false;

        Assert.Equal(dark, KeptScheme.StartsDark(scheme, () => { asked = true; return osDark; }));
        Assert.Equal(scheme == ColorScheme.System, asked);
    }

    /// <summary>
    /// Held on the application's own source, as the colour scheme's route is (THEMESCHEME1), because the window's
    /// composition has no test project: the kept choice seeds the kit's host options, is kept on each change, and the
    /// main window and each secondary window start their palette from the app's scheme rather than from the OS.
    /// </summary>
    [Fact]
    public void The_application_starts_from_the_kept_choice_and_keeps_each_change()
    {
        var program = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Program.cs"));
        var windows = program[program.IndexOf("builder.UseWindows(new WindowsHostOptions", StringComparison.Ordinal)..];
        Assert.Contains("ColorScheme = KeptScheme.Read(keptScheme)", windows[..windows.IndexOf("});", StringComparison.Ordinal)]);
        Assert.Contains("KeptScheme.Keep(", program);

        var main = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "MainForm.cs"));
        Assert.DoesNotContain("ChromePalette.For(OperatingSystemPrefersDark())", main);
        Assert.Contains("KeptScheme.StartsDark(", main);

        var secondary = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "SecondaryForm.cs"));
        Assert.DoesNotContain("MainForm.OperatingSystemPrefersDark()", secondary);
    }
}
