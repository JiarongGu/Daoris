using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What finishes a folder place: the declaration it holds at its root read from disk, the person's own excludes beside the defaults, a
/// registration composed for it from the terminal, and the manifest read when it is added so its first turn has a base.
/// </summary>
public sealed class FolderPlaceRestTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("folder-place-rest");
    private string Place => Path.Combine(_scratch, "designs");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private void Write(string path, string text)
    {
        var full = Path.Combine(Place, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private const string Declares =
        """{"source":"canon","domain":{"summary":"Design notes","owns":["notes"],"accepts":["a note"]}}""";

    [Fact]
    public void A_folder_s_declaration_is_read_from_its_working_files()
    {
        Write("daoris.json", Declares);
        Write("daoris.lanes.json", """{"lanes":[]}""");

        var files = FolderDeclarationReader.Read(Place);

        Assert.Null(files.Problem);
        Assert.Null(files.Commit);
        Assert.Equal(Declares, files.Manifest);
        Assert.Equal("""{"lanes":[]}""", files.Lanes);
    }

    [Fact]
    public void A_folder_with_no_declaration_reads_none_and_one_that_is_a_folder_is_said()
    {
        Assert.Null(FolderDeclarationReader.Read(Place).Manifest);

        Directory.CreateDirectory(Path.Combine(Place, "daoris.json"));
        var files = FolderDeclarationReader.Read(Place);

        Assert.Null(files.Manifest);
        Assert.Contains("not a file", files.FileProblem);
    }

    [Fact]
    public void The_person_s_own_excludes_are_passed_by_beside_the_defaults()
    {
        Write("notes.md", "text\n");
        Write("build/out.txt", "built");
        Write("deep/build/out.txt", "built");
        Write("debug.log", "log");
        Write("scratch.tmp", "tmp");
        var excludes = PlaceExcludes.Of(["build/**", "*.log"]);
        var store = new ManifestStore(Home, "default", "designs");

        var manifest = FolderManifestReader.Read(Place, store.Content, null, ManifestBounds.Default, excludes);

        Assert.Equal(["deep/build/out.txt", "notes.md"], manifest.Entries.Select(entry => entry.Path));
    }

    [Fact]
    public async Task A_folder_turn_passes_by_the_registration_s_excludes()
    {
        Write("notes.md", "text\n");
        Write("debug.log", "log");

        await PlaceTurns.StartAsync(Home, "default", "designs", Place, "s-aaaaaaaa", null, CancellationToken.None, PlaceExcludes.Of(["*.log"]));

        var start = new ManifestStore(Home, "default", "designs").Read("s-aaaaaaaa", ManifestMoment.Start)!;
        Assert.Equal(["notes.md"], start.Entries.Select(entry => entry.Path));
    }

    [Fact]
    public void A_folder_s_registration_says_its_kind_root_excludes_and_what_it_declares()
    {
        Write("daoris.json", Declares);

        var plan = FolderAdd.Plan(Place, "work", ["*.log"], "http://127.0.0.1:7777");

        Assert.Equal("designs", plan.Repository);
        Assert.Equal("folder", (string?)plan.Body["kind"]);
        Assert.Equal(Place, (string?)plan.Body["root"]);
        Assert.Equal("work", (string?)plan.Body["workspace"]);
        Assert.Equal(["*.log"], ((JsonArray)plan.Body["excludes"]!).Select(each => (string)each!));
        Assert.Equal("Design notes", (string?)plan.Body["domain"]!["summary"]);
    }

    [Fact]
    public void A_folder_with_no_declaration_is_registered_with_nothing_declared_and_a_git_checkout_is_refused()
    {
        Directory.CreateDirectory(Place);
        var plan = FolderAdd.Plan(Place, null, [], "http://127.0.0.1:7777");

        Assert.Equal("folder", (string?)plan.Body["kind"]);
        Assert.False((bool)plan.Body["adopted"]!);
        Assert.Null(plan.Body["workspace"]);
        Assert.Null(plan.Body["excludes"]);

        Directory.CreateDirectory(Path.Combine(Place, ".git"));
        var refused = Assert.Throws<RegistryRefusal>(() => FolderAdd.Plan(Place, null, [], "http://127.0.0.1:7777"));
        Assert.Contains("git", refused.Message);
    }

    [Fact]
    public void A_folder_s_root_goes_only_to_a_service_on_this_machine()
    {
        Directory.CreateDirectory(Place);

        var plan = FolderAdd.Plan(Place, null, [], "https://shared.example.com");

        Assert.Null(plan.Body["root"]);
    }

    [Fact]
    public async Task The_manifest_read_at_the_add_is_the_first_turn_s_base()
    {
        Write("notes.md", "first\n");
        FolderFirstManifest.Keep(Home, "default", "designs", Place, null);
        Write("notes.md", "the person's, before any session\n");
        Write("new.md", "new\n");

        var told = await PlaceTurns.StartAsync(Home, "default", "designs", Place, "s-aaaaaaaa", null, CancellationToken.None);

        Assert.Equal(["new.md", "notes.md"], told!.Outside);
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_refused_before_anything_is_registered()
    {
        var refused = Assert.Throws<RegistryRefusal>(() => FolderAdd.Plan(Path.Combine(Place, "missing"), null, [], "http://127.0.0.1:7777"));

        Assert.Contains("not a folder", refused.Message);
    }
}
