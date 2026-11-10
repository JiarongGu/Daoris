using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Adding a system department from the terminal: the registration names its kind and its adapter's system, never a root, and is
/// refused where the word names no system or no plugin installed and on adapts it.
/// </summary>
public sealed class SystemAddTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-add-" + Guid.NewGuid().ToString("N")[..8]);

    public SystemAddTests()
    {
        var source = Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", "stand-in-system");
        var installed = DaorisHome.Entry(_home, HomeEntry.Plugins, "stand-in-system");
        Directory.CreateDirectory(installed);
        File.Copy(Path.Combine(source, "plugin.json"), Path.Combine(installed, "plugin.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_system_is_registered_by_its_adapter_s_system_with_no_root()
    {
        var plan = SystemAdd.Plan(_home, "stand-in-system/stand-in", "store", "work", "http://127.0.0.1:5000");

        Assert.Equal("store", plan.Repository);
        Assert.Equal("system", (string?)plan.Body["kind"]);
        Assert.Equal("stand-in-system/stand-in", (string?)plan.Body["system"]);
        Assert.Equal("work", (string?)plan.Body["workspace"]);
        Assert.False((bool?)plan.Body["adopted"]);
        Assert.Null(plan.Body["root"]);
    }

    [Fact]
    public void Its_name_is_the_system_s_id_where_none_is_given()
    {
        Assert.Equal("stand-in", SystemAdd.Plan(_home, "stand-in-system/stand-in", null, null, "http://127.0.0.1:5000").Repository);
    }

    [Theory]
    [InlineData("stand-in-system", "names no system")]
    [InlineData("stand-in-system/elsewhere", "no plugin installed and on adapts")]
    [InlineData("other-plugin/stand-in", "no plugin installed and on adapts")]
    public void A_word_naming_no_installed_system_is_refused(string word, string said)
    {
        var refused = Assert.Throws<RegistryRefusal>(() => SystemAdd.Plan(_home, word, null, null, "http://127.0.0.1:5000"));
        Assert.Contains(said, refused.Message, StringComparison.Ordinal);
    }
}
