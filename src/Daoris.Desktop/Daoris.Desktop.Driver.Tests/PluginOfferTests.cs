using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Daoris's own example plugins, offered by the install (PLUG9 d, D102): the driver's half of a twin with
/// the CLI's <c>plugins.ts</c>, whose <c>plugin-sources.test.ts</c> holds the same table. The offers are the
/// install's <c>app/plugin-offers/</c>, read as the catalogue reads a plugin with the placeholders as
/// written, each with what its README says it needs. None is installed until a press.
/// </summary>
public sealed class PluginOfferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-plugin-offers-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Offers => Path.Combine(_root, "app", "plugin-offers");

    private static IReadOnlyCollection<string> Reserved => AdapterSet.Built().Names;

    public PluginOfferTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Offered = """
        { "id": "github-pull-request", "name": "GitHub pull request", "version": "1.0.0",
          "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }
        """;

    private const string Readme = """
        # github-pull-request

        Intro.

        ## What it needs

        - **node** on the PATH
          (it is a `node` script).
        - **gh**, signed in: `gh auth login`.

        ## When something fails

        - not a need

        """;

    private static void Folder(string at, string manifest, string? readme = null)
    {
        Directory.CreateDirectory(at);
        File.WriteAllText(Path.Combine(at, PluginCatalog.ManifestName), manifest);
        if (readme is not null) File.WriteAllText(Path.Combine(at, "README.md"), readme.Replace("\r\n", "\n"));
    }

    [Fact]
    public void The_offers_are_the_installs_app_plugin_offers_beside_the_home_or_beside_the_application()
    {
        Assert.Equal(Offers, PluginOffers.BesideHome(Home));
        Assert.Equal(Offers, PluginOffers.BesideHome(Home + Path.DirectorySeparatorChar));
        Assert.Equal(Path.Combine(_root, "app", "plugin-offers"), PluginOffers.BesideApplication(Path.Combine(_root, "app")));
        Assert.Equal(["app", "plugin-offers"], PluginOffers.Layout);
        Assert.Empty(PluginOffers.Load(Offers, Home, Reserved));
        Assert.Empty(PluginOffers.Load(null, Home, Reserved));
    }

    /// <summary>The README's requirement lines: the CLI's <c>what an offer needs…</c>, row for row.</summary>
    [Fact]
    public void What_an_offer_needs_is_its_readmes_own_section_emphasis_dropped_and_code_kept()
    {
        IReadOnlyList<string> At(string? text)
        {
            var plugin = Path.Combine(_root, Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(plugin);
            if (text is not null) File.WriteAllText(Path.Combine(plugin, "README.md"), text);
            return PluginOffers.Needs(plugin);
        }

        string[] needs = ["node on the PATH (it is a `node` script).", "gh, signed in: `gh auth login`."];
        Assert.Empty(At(null));
        Assert.Empty(At("# x\n\nNothing about needs.\n"));
        Assert.Equal(needs, At(Readme.Replace("\r\n", "\n")));
        Assert.Equal(needs, At(Readme.Replace("\r\n", "\n").Replace("\n", "\r\n")));
    }

    [Fact]
    public void The_offers_are_listed_with_what_they_declare_and_need_and_an_unsound_one_says_why()
    {
        Folder(Path.Combine(Offers, "github-pull-request"), Offered, Readme);
        Folder(Path.Combine(Offers, "future"), """{ "id": "future", "apiVersion": 99 }""");
        Folder(Path.Combine(Offers, "shadow"), """{ "id": "shadow", "harnesses": [ { "name": "dsh", "command": ["x"] } ] }""");
        Folder(Path.Combine(Offers, ".hidden"), Offered);
        Directory.CreateDirectory(Path.Combine(Offers, "notes"));

        var listed = PluginOffers.Load(Offers, Home, Reserved);

        Assert.Equal(["future", "github-pull-request", "shadow"], listed.Select(offer => offer.Id));
        var github = listed.Single(offer => offer.Id == "github-pull-request");
        Assert.Null(github.Problem);
        Assert.False(github.Installed);
        // As the manifest writes it: `${plugin}` stays, never a path on this machine.
        Assert.Equal(["node", "${plugin}/land.mjs"], github.Manifest.Hooks!.Command);
        Assert.Equal(["node on the PATH (it is a `node` script).", "gh, signed in: `gh auth login`."], github.Needs);
        Assert.Contains("needs plugin API 99", listed.Single(offer => offer.Id == "future").Problem);
        Assert.Contains("which this build already carries", listed.Single(offer => offer.Id == "shadow").Problem);
    }

    [Fact]
    public void An_offer_installed_here_is_marked_installed()
    {
        Folder(Path.Combine(Offers, "github-pull-request"), Offered);
        Folder(Path.Combine(Home, PluginCatalog.Folder, "github-pull-request"), Offered);

        Assert.True(Assert.Single(PluginOffers.Load(Offers, Home, Reserved)).Installed);
    }

    /// <summary>
    /// The install's own offers, as the tracked examples stand (D102): the two that land work and the one that
    /// hands a session Daoris's own browser, each sound here and saying what it needs. The rehearsal fixture
    /// (<c>hold-by-title</c>) and the browser a machine without the shell uses are not offered.
    /// </summary>
    [Fact]
    public void The_tracked_examples_that_are_offered_are_sound_and_say_what_they_need()
    {
        var examples = Path.Combine(Repository(), "examples", "plugins");

        foreach (var id in new[] { "github-pull-request", "azure-devops-pull-request", "in-app-browser" })
        {
            var (manifest, problem) = PluginCatalog.ReadAsWritten(id, Path.Combine(examples, id, PluginCatalog.ManifestName));
            Assert.Null(problem);
            Assert.Null(PluginCatalog.RefusedByThisBuild(manifest, Reserved));
            Assert.NotEmpty(PluginOffers.Needs(Path.Combine(examples, id)));
        }

        Assert.Contains(PluginOffers.Needs(Path.Combine(examples, "github-pull-request")), need => need.Contains("`gh auth login`", StringComparison.Ordinal));
        Assert.Contains(PluginOffers.Needs(Path.Combine(examples, "azure-devops-pull-request")), need => need.Contains("`az login`", StringComparison.Ordinal)
            && need.Contains("`az extension add --name azure-devops`", StringComparison.Ordinal));
    }

    private static string Repository()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "daoris.json"))) here = here.Parent;
        return here?.FullName ?? throw new InvalidOperationException("the repository root is not above the test's folder");
    }
}
