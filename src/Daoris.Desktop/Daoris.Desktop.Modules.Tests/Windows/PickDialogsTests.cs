using Shenora.Modules.FileDialog;
using Xunit;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>What each kind of file the picker asks for is shown with: its title and its filters, the first the one chosen on opening.</summary>
public sealed class PickDialogsTests
{
    /// <summary>The kit's Windows filter string for these rows (its rule: every extension is written <c>*.ext</c>).</summary>
    private static string Spec(FileDialogFilter filter) => string.Join(";", filter.Extensions.Select(extension => $"*.{extension}"));

    /// <summary>A program is shown under *Programs* first, then every file, and without a title is asked for as a program.</summary>
    [Fact]
    public void A_program_opens_on_programs_then_all_files()
    {
        var dialog = PickDialogs.For(PickKind.Program, null);
        Assert.Equal("Choose a program", dialog.Title);
        Assert.Equal(["Programs", "All files"], dialog.Filters.Select(filter => filter.Name));
        Assert.Equal("*.exe;*.cmd;*.bat", Spec(dialog.Filters[0]));
        Assert.Equal("*.*", Spec(dialog.Filters[1]));
    }

    /// <summary>A key is shown under its usual names first (the extensionless <c>id_ed25519</c> too), then every file.</summary>
    [Fact]
    public void A_key_opens_on_its_usual_names_then_all_files()
    {
        var dialog = PickDialogs.For(PickKind.Key, null);
        Assert.Equal("Choose a key file", dialog.Title);
        Assert.Equal(["Keys", "All files"], dialog.Filters.Select(filter => filter.Name));
        var names = Spec(dialog.Filters[0]).Split(';');
        Assert.Equal(["*.pem", "*.ppk", "*.key", "id_ed25519", "id_ecdsa", "id_rsa", "id_dsa"], names);
        Assert.DoesNotContain("*.exe", names);
        Assert.Equal("*.*", Spec(dialog.Filters[1]));
    }

    /// <summary>A file is any file: one filter, every file.</summary>
    [Fact]
    public void A_file_is_any_file()
    {
        var dialog = PickDialogs.For(PickKind.File, null);
        Assert.Equal("Choose a file", dialog.Title);
        var only = Assert.Single(dialog.Filters);
        Assert.Equal("All files", only.Name);
        Assert.Equal("*.*", Spec(only));
    }

    /// <summary>The page's own title, in its language, wins; a blank one is none.</summary>
    [Theory]
    [InlineData(PickKind.Program, "Choose Git", "Choose Git")]
    [InlineData(PickKind.Key, "选择密钥文件", "选择密钥文件")]
    [InlineData(PickKind.File, "  ", "Choose a file")]
    public void The_pages_title_wins_and_a_blank_one_is_none(PickKind kind, string title, string expected) =>
        Assert.Equal(expected, PickDialogs.For(kind, title).Title);

    /// <summary>The wire's word names the kind; none or an unknown one is a program.</summary>
    [Theory]
    [InlineData("program", PickKind.Program)]
    [InlineData("key", PickKind.Key)]
    [InlineData("file", PickKind.File)]
    [InlineData("KEY", PickKind.Key)]
    [InlineData(null, PickKind.Program)]
    [InlineData("nonsense", PickKind.Program)]
    public void The_wires_word_names_the_kind(string? word, PickKind expected) => Assert.Equal(expected, PickDialogs.Kind(word));
}
