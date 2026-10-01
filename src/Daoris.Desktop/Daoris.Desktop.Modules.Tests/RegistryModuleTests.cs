using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The one thing a browser genuinely cannot do: name a directory on this machine (D48 §7).
/// </summary>
/// <remarks>
/// Everything else about managing a repository is an ordinary loopback call, deliberately — so this
/// module is small, and the part worth testing hardest is the one write it performs: editing a
/// repository's own tracked `daoris.json` through a form.
/// </remarks>
public sealed class RegistryModuleTests : Bridge
{
    private string? _picked;

    private RegistryModule Module() => new(Bus, () => _picked);

    /// <summary>A checkout in this test's own scratch — never a real repository on this machine.</summary>
    private string Repository(string name, object? manifest = null)
    {
        var path = Path.Combine(Home, name);
        Directory.CreateDirectory(path);
        if (manifest is not null)
        {
            File.WriteAllText(
                Path.Combine(path, "daoris.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        return path;
    }

    [Fact]
    public async Task Cancelling_the_folder_dialog_is_an_answer_and_not_a_failure()
    {
        _picked = null;

        var response = await AskAsync(Module(), "PICK_FOLDER");

        // A person who changed their mind has not caused an error, and a page that showed one would
        // be scolding them for it.
        Assert.True(response.Success);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task A_folder_that_has_not_adopted_is_reported_rather_than_refused()
    {
        _picked = Repository("stranger");

        var found = await AnswerAsync(Module(), "PICK_FOLDER");

        Assert.False(found.GetProperty("adopted").GetBoolean());
        Assert.True(found.GetProperty("exists").GetBoolean());
        Assert.Equal("stranger", found.GetProperty("name").GetString());
    }

    /// <summary>
    /// A path that does not exist is a fact about a folder, not a crash: a checkout can be moved
    /// between the pick and the look.
    /// </summary>
    [Fact]
    public async Task A_folder_that_is_not_there_answers_plainly()
    {
        _picked = Path.Combine(Home, "no-such-folder");
        var found = await AnswerAsync(Module(), "PICK_FOLDER");

        Assert.False(found.GetProperty("exists").GetBoolean());
        Assert.False(found.GetProperty("adopted").GetBoolean());
    }

    [Fact]
    public async Task An_adopted_folder_answers_what_it_declared_about_itself()
    {
        _picked = Repository("engine", new
        {
            source = "github:JiarongGu/Daoris#v0.0.1",
            packs = new[] { "dotnet" },
            domain = new { summary = "the engine runtime", owns = new[] { "rendering" }, accepts = new[] { "a bug" } },
            remote = new { join = true, knowledge = true },
        });

        var found = await AnswerAsync(Module(), "PICK_FOLDER");

        Assert.True(found.GetProperty("adopted").GetBoolean());
        Assert.Equal("the engine runtime", found.GetProperty("summary").GetString());
        Assert.True(found.GetProperty("join").GetBoolean());
        Assert.True(found.GetProperty("shareKnowledge").GetBoolean());
    }

    /// <summary>
    /// Knowledge feeds only from a joined repository (D47 §4), narrowed wherever a manifest is READ —
    /// because this file is written by hand as often as by the CLI, and a hand-written
    /// `knowledge: true` without `join` must not read as sharing.
    /// </summary>
    [Fact]
    public async Task Knowledge_without_join_reads_as_not_sharing()
    {
        _picked = Repository("hopeful", new
        {
            source = "s",
            remote = new { join = false, knowledge = true },
        });

        var found = await AnswerAsync(Module(), "PICK_FOLDER");

        Assert.False(found.GetProperty("join").GetBoolean());
        Assert.False(found.GetProperty("shareKnowledge").GetBoolean());
    }

    /// <summary>
    /// <b>The trap this write most easily falls into.</b> The manifest carries `source`, `packs`,
    /// `target`, `coreBudgetBytes` and whatever a later version adds; a form that serialized only the
    /// fields it knows about would silently delete the rest, and the person would find out at their
    /// next `sync` — with no diff to explain it, because the form "succeeded".
    /// </summary>
    [Fact]
    public async Task Writing_a_declaration_keeps_every_field_the_form_does_not_own()
    {
        var path = Repository("engine", new
        {
            source = "github:JiarongGu/Daoris#v0.0.1",
            packs = new[] { "dotnet", "web-webview" },
            target = ".claude",
            coreBudgetBytes = 30000,
            somethingANewerBuildWrote = new { keep = "me" },
            domain = new { summary = "before", owns = Array.Empty<string>(), accepts = Array.Empty<string>() },
        });

        await AnswerAsync(Module(), "WRITE_DECLARATION", new
        {
            path,
            summary = "after",
            owns = new[] { "rendering" },
            accepts = new[] { "a playtest finding" },
            join = true,
            shareKnowledge = true,
        });

        using var written = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "daoris.json")));
        var root = written.RootElement;
        Assert.Equal("github:JiarongGu/Daoris#v0.0.1", root.GetProperty("source").GetString());
        Assert.Equal(30000, root.GetProperty("coreBudgetBytes").GetInt32());
        Assert.Equal(2, root.GetProperty("packs").GetArrayLength());
        Assert.Equal("me", root.GetProperty("somethingANewerBuildWrote").GetProperty("keep").GetString());
        // …and the two blocks the form DOES own are replaced, not merged into.
        Assert.Equal("after", root.GetProperty("domain").GetProperty("summary").GetString());
        Assert.Equal("rendering", root.GetProperty("domain").GetProperty("owns")[0].GetString());
    }

    /// <summary>
    /// D91: what a repository says it uses sits in the same block the form writes, and the form has no
    /// field for it. Rewriting the block from its three fields deleted it, and nobody saw it go.
    /// </summary>
    [Fact]
    public async Task Writing_a_declaration_keeps_what_the_domain_says_it_uses()
    {
        var path = Repository("game", new
        {
            source = "s",
            domain = new { summary = "before", owns = Array.Empty<string>(), accepts = Array.Empty<string>(), uses = new[] { "engine" } },
        });

        await AnswerAsync(Module(), "WRITE_DECLARATION", new
        {
            path,
            summary = "after",
            owns = new[] { "gameplay" },
            accepts = Array.Empty<string>(),
        });

        using var written = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "daoris.json")));
        var domain = written.RootElement.GetProperty("domain");
        Assert.Equal("after", domain.GetProperty("summary").GetString());
        Assert.Equal("engine", domain.GetProperty("uses")[0].GetString());
    }

    /// <summary>
    /// MANAGE1: the page re-registers from what this write answers, and the registry replaces a row's
    /// `uses` with the declaration's (D91), where saying none is saying nothing. The answer left `uses`
    /// out, so saving a declaration erased the repository's dependencies from the registry until its next
    /// `connect`, while the file the form had just written still held them.
    /// </summary>
    [Fact]
    public async Task Writing_a_declaration_answers_what_the_domain_says_it_uses()
    {
        var path = Repository("game", new
        {
            source = "s",
            domain = new { summary = "before", owns = Array.Empty<string>(), accepts = Array.Empty<string>(), uses = new[] { "engine", "tools" } },
        });

        var written = await AnswerAsync(Module(), "WRITE_DECLARATION", new
        {
            path,
            summary = "after",
            owns = Array.Empty<string>(),
            accepts = Array.Empty<string>(),
        });

        Assert.Equal(
            new[] { "engine", "tools" },
            written.GetProperty("uses").EnumerateArray().Select(name => name.GetString()!).ToArray());
    }

    /// <summary>
    /// The add reads the same answer (MANAGE1): a folder whose manifest uses nothing answers an empty
    /// list, which is what its file says, and one that never adopted has nothing to say.
    /// </summary>
    [Fact]
    public async Task A_picked_folder_answers_what_it_uses_and_none_is_an_empty_list()
    {
        _picked = Repository("game", new { source = "s", domain = new { summary = "s", uses = new[] { "engine" } } });
        var game = await AnswerAsync(Module(), "PICK_FOLDER");
        Assert.Equal("engine", Assert.Single(game.GetProperty("uses").EnumerateArray()).GetString());

        _picked = Repository("engine", new { source = "s", domain = new { summary = "s" } });
        Assert.Equal(0, (await AnswerAsync(Module(), "PICK_FOLDER")).GetProperty("uses").GetArrayLength());

        _picked = Repository("stranger");
        Assert.Equal(0, (await AnswerAsync(Module(), "PICK_FOLDER")).GetProperty("uses").GetArrayLength());
    }

    /// <summary>Knowledge without join is a manifest the CLI refuses; a form must not write one.</summary>
    [Fact]
    public async Task A_form_cannot_write_knowledge_without_join()
    {
        var path = Repository("engine", new { source = "s" });

        await AnswerAsync(Module(), "WRITE_DECLARATION", new
        {
            path,
            summary = "s",
            owns = Array.Empty<string>(),
            accepts = Array.Empty<string>(),
            join = false,
            shareKnowledge = true,
        });

        using var written = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "daoris.json")));
        var remote = written.RootElement.GetProperty("remote");
        Assert.False(remote.GetProperty("join").GetBoolean());
        Assert.False(remote.GetProperty("knowledge").GetBoolean());
    }

    /// <summary>
    /// The file is tracked, so it is written LF and BOM-less like every Daoris write — a line-ending
    /// flip reads as a whole-file diff in the repository this form just edited.
    /// </summary>
    [Fact]
    public async Task The_manifest_is_written_lf_and_bom_less()
    {
        var path = Repository("engine", new { source = "s" });

        await AnswerAsync(Module(), "WRITE_DECLARATION", new
        {
            path, summary = "s", owns = Array.Empty<string>(), accepts = Array.Empty<string>(),
            join = false, shareKnowledge = false,
        });

        var bytes = File.ReadAllBytes(Path.Combine(path, "daoris.json"));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "the manifest was written with a BOM");
        Assert.Equal((byte)'\n', bytes[^1]);
    }

    /// <summary>
    /// Adoption is the repository's own agent's job — `init`, `sync`, the collision review. A shell
    /// that wrote a first manifest would be doing that work from outside, badly.
    /// </summary>
    [Fact]
    public async Task Writing_into_a_folder_that_never_adopted_is_refused_naming_it()
    {
        var path = Repository("stranger");

        var refusal = await RefusalAsync(Module(), "WRITE_DECLARATION", new
        {
            path, summary = "s", owns = Array.Empty<string>(), accepts = Array.Empty<string>(),
            join = false, shareKnowledge = false,
        });

        Assert.Contains(Refusals.RepositoryNotAdopted, refusal);
        Assert.Contains("repository=stranger", refusal);
        Assert.False(File.Exists(Path.Combine(path, "daoris.json")), "a refused write still created a manifest");
    }

    /// <summary>
    /// A manifest that will not parse is adopted-but-unreadable: the repository's own tooling will say
    /// why, and the person still needs to see that something is there rather than a crash.
    /// </summary>
    [Fact]
    public async Task An_unparsable_manifest_reads_as_adopted_rather_than_failing()
    {
        var path = Repository("broken");
        File.WriteAllText(Path.Combine(path, "daoris.json"), "{ half a manifest");
        _picked = path;

        var found = await AnswerAsync(Module(), "PICK_FOLDER");

        Assert.True(found.GetProperty("adopted").GetBoolean());
        Assert.Equal(0, found.GetProperty("owns").GetArrayLength());
    }
}
