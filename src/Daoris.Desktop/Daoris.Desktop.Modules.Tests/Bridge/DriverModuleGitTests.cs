using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// GIT1d-a (D147 §2, D150): the Git reads' routes for Repositories' Branches and History tabs. <c>GIT_BRANCHES</c> is the
/// driver's list and <c>GIT_GRAPH</c>, <c>GIT_COMMIT</c>, <c>GIT_HISTORY</c>, <c>GIT_BLAME</c> and <c>GIT_COMPARE</c> its reads,
/// each of the repository's <i>name</i>, whose root the registry gives. A read the driver does not answer is an answer with a
/// <c>refusal</c> code the page words (D147), and no answer carries a machine path (D47 §4): a branch's tree by its folder's
/// name, and a sentence that named the root says the repository instead.
/// </summary>
/// <remarks>The service is a stand-in reached through the real client; the cases that run git are <c>Process</c>.</remarks>
public sealed class DriverModuleGitTests : DriverModuleBridge
{
    private const string Whole = "0123456789abcdef0123456789abcdef01234567";

    private string Checkout(string name, bool git = false)
    {
        var root = Path.Combine(Home, "checkouts", name);
        Directory.CreateDirectory(git ? Path.Combine(root, ".git") : root);
        return root;
    }

    private async Task<DriverModule> UpAsync(string? root)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(root))));
        return new DriverModule(Bus, loop);
    }

    [Theory]
    [InlineData("GIT_BRANCHES")]
    [InlineData("GIT_GRAPH")]
    [InlineData("GIT_COMMIT")]
    [InlineData("GIT_HISTORY")]
    [InlineData("GIT_BLAME")]
    [InlineData("GIT_COMPARE")]
    [InlineData("CHECKOUT_BRANCH")]
    public async Task Before_the_driver_is_up_each_read_is_the_cold_start_sentence(string route)
    {
        var payload = new { repository = "engine", tips = new[] { Whole }, commit = Whole, left = Whole, right = Whole, path = "a.txt" };
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), route, payload));
    }

    [Fact]
    public async Task A_read_names_its_repository()
    {
        var module = await UpAsync(null);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "GIT_COMMIT", new { commit = Whole }));
    }

    [Fact]
    public async Task A_repository_with_no_checkout_here_is_answered_with_its_code()
    {
        var module = await UpAsync(null);

        var answer = await AnswerAsync(module, "GIT_COMMIT", new { repository = "engine", commit = Whole });

        Assert.Equal(Refusals.GitNoCheckout, answer.GetProperty("refusal").GetString());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task The_checkouts_branch_read_names_its_repository()
    {
        var module = await UpAsync(null);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "CHECKOUT_BRANCH", new { }));
    }

    [Fact]
    public async Task The_checkouts_branch_of_a_repository_with_no_checkout_is_refused_with_its_code()
    {
        var module = await UpAsync(null);

        Assert.Contains(Refusals.GitNoCheckout, await RefusalAsync(module, "CHECKOUT_BRANCH", new { repository = "engine" }));
    }

    /// <summary>The checkout's head: its branch's short name, or null where detached; no path in the answer (D47 §4).</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_checkouts_branch_is_its_name_or_null_where_detached()
    {
        var root = await InitAsync();
        var module = await UpAsync(root);

        var onBranch = await AnswerAsync(module, "CHECKOUT_BRANCH", new { repository = "engine", workspace = "aurora" });
        Assert.Equal("main", onBranch.GetProperty("branch").GetString());
        Assert.DoesNotContain("checkouts", onBranch.GetRawText());

        await GitAsync(root, "checkout", "--quiet", "--detach", "HEAD");
        var detached = await AnswerAsync(module, "CHECKOUT_BRANCH", new { repository = "engine" });
        Assert.Equal(JsonValueKind.Null, detached.GetProperty("branch").ValueKind);
    }

    /// <summary>GITAPP2b: the Changes read answers a staged-and-removed file in both lists, `readAt` saying when, and no path.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_changes_read_answers_a_staged_and_removed_file_twice_and_says_when()
    {
        var root = await InitAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "gone.txt"), "x\n");
        await GitAsync(root, "add", "gone.txt");
        File.Delete(Path.Combine(root, "gone.txt"));
        var module = await UpAsync(root);

        var answer = await AnswerAsync(module, "GIT_CHANGES", new { repository = "engine", workspace = "aurora" });

        var value = answer.GetProperty("value");
        Assert.Equal("gone.txt", value.GetProperty("staged")[0].GetProperty("path").GetString());
        Assert.Equal("gone.txt", value.GetProperty("unstaged")[0].GetProperty("path").GetString());
        Assert.Equal(JsonValueKind.String, value.GetProperty("readAt").ValueKind);
        Assert.DoesNotContain("checkouts", answer.GetRawText());
    }

    /// <summary>GITAPP2h: a file's hunks come with the ids a press sends back, and a stage with the switch off is refused as data, with no path.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_hunks_read_names_the_ids_and_a_stage_with_the_switch_off_is_refused_in_words()
    {
        var root = await InitAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "changed\n");
        var module = await UpAsync(root);

        var hunks = await AnswerAsync(module, "GIT_HUNKS", new { repository = "engine", workspace = "aurora", path = "README.md", side = "unstaged" });
        var value = hunks.GetProperty("value");
        var press = new { repository = "engine", workspace = "aurora", path = "README.md", head = value.GetProperty("head").GetString(), index = value.GetProperty("index").GetString(), source = value.GetProperty("source").GetString() };
        var refused = await AnswerAsync(module, "GIT_STAGE", press);
        var undo = await AnswerAsync(module, "GIT_UNDO", new { repository = "engine", workspace = "aurora", act = "none" });

        Assert.Equal(1, value.GetProperty("hunks").GetArrayLength());
        Assert.Matches("^[0-9a-f]{12}$", value.GetProperty("hunks")[0].GetProperty("id").GetString());
        Assert.False(refused.GetProperty("done").GetBoolean());
        Assert.True(refused.GetProperty("refused").GetBoolean());
        Assert.Contains("may not change", refused.GetProperty("said").GetString());
        Assert.DoesNotContain(root, refused.GetRawText().Replace("\\\\", "\\"));
        Assert.True(undo.GetProperty("refused").GetBoolean());
        Assert.Equal("changed\n", await File.ReadAllTextAsync(Path.Combine(root, "README.md")));
    }

    /// <summary>GITAPP2i: the commit's plan names the tip, the tree and who commits; a commit with the switch off is refused as data, with no path, and HEAD stays.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_commit_plan_names_the_tree_and_a_commit_with_the_switch_off_is_refused_in_words()
    {
        var root = await InitAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "changed\n");
        await GitAsync(root, "add", "README.md");
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var module = await UpAsync(root);

        var plan = (await AnswerAsync(module, "GIT_COMMIT_PLAN", new { repository = "engine", workspace = "aurora", verb = "commit" })).GetProperty("value");
        var press = new { repository = "engine", workspace = "aurora", message = "changed", head = plan.GetProperty("head").GetString(), tree = plan.GetProperty("tree").GetString() };
        var refused = await AnswerAsync(module, "GIT_MAKE_COMMIT", press);
        var amend = await AnswerAsync(module, "GIT_AMEND", press);

        Assert.Equal(before, plan.GetProperty("head").GetString());
        Assert.Matches("^[0-9a-f]{40,64}$", plan.GetProperty("tree").GetString());
        Assert.False(plan.GetProperty("empty").GetBoolean());
        Assert.True(refused.GetProperty("refused").GetBoolean());
        Assert.Contains("may not change", refused.GetProperty("said").GetString());
        Assert.DoesNotContain(root, refused.GetRawText().Replace("\\\\", "\\"));
        Assert.True(amend.GetProperty("refused").GetBoolean());
        Assert.Equal(before, (await GitAsync(root, "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task The_changes_read_of_a_repository_with_no_checkout_is_answered_with_its_code()
    {
        var module = await UpAsync(null);

        var answer = await AnswerAsync(module, "GIT_CHANGES", new { repository = "engine" });

        Assert.Equal(Refusals.GitNoCheckout, answer.GetProperty("refusal").GetString());
    }

    /// <summary>A commit named by anything but its whole id is the driver's refusal, coded; git is not asked.</summary>
    [Fact]
    public async Task A_name_that_is_not_a_whole_commit_is_refused_with_its_code()
    {
        var module = await UpAsync(Checkout("engine", git: true));

        Assert.Equal(Refusals.GitNotACommit, (await AnswerAsync(module, "GIT_COMMIT", new { repository = "engine", commit = "main" })).GetProperty("refusal").GetString());
        Assert.Equal(Refusals.GitNotACommit, (await AnswerAsync(module, "GIT_GRAPH", new { repository = "engine", tips = Array.Empty<string>() })).GetProperty("refusal").GetString());
        Assert.Equal(Refusals.GitNotACommit, (await AnswerAsync(module, "GIT_COMPARE", new { repository = "engine", left = Whole, right = "main" })).GetProperty("refusal").GetString());
        Assert.Equal(Refusals.GitNotAPath, (await AnswerAsync(module, "GIT_BLAME", new { repository = "engine", commit = Whole, path = "../x" })).GetProperty("refusal").GetString());
        Assert.Equal(Refusals.GitNotAPath, (await AnswerAsync(module, "GIT_HISTORY", new { repository = "engine", commit = Whole, path = "" })).GetProperty("refusal").GetString());
        Assert.Equal(Refusals.GitNotAPage, (await AnswerAsync(module, "GIT_HISTORY", new { repository = "engine", commit = Whole, path = "a.txt", page = -1 })).GetProperty("refusal").GetString());
        Assert.Equal(Refusals.GitTooManyTips, (await AnswerAsync(module, "GIT_GRAPH",
            new { repository = "engine", tips = Enumerable.Range(0, GitReads.GraphTips + 1).Select(n => n.ToString("x40")).ToArray() })).GetProperty("refusal").GetString());
    }

    /// <summary>🔴 A root that is not a repository's top is refused, and the sentence never names the machine's path (D47 §4).</summary>
    [Fact]
    public async Task A_folder_that_is_not_a_repositorys_top_is_refused_without_its_path()
    {
        var module = await UpAsync(Checkout("engine"));

        var answer = await AnswerAsync(module, "GIT_COMMIT", new { repository = "engine", commit = Whole });

        Assert.Equal(Refusals.GitNotTop, answer.GetProperty("refusal").GetString());
        Assert.DoesNotContain("checkouts", answer.GetRawText());
    }

    [Fact]
    public async Task The_list_of_a_folder_that_is_not_a_repositorys_top_names_no_path()
    {
        var module = await UpAsync(Checkout("engine"));

        var answer = await AnswerAsync(module, "GIT_BRANCHES", new { all = true });

        var repository = Assert.Single(answer.GetProperty("repositories").EnumerateArray());
        Assert.Equal("engine", repository.GetProperty("repository").GetString());
        Assert.False(string.IsNullOrEmpty(repository.GetProperty("problem").GetString()));
        Assert.DoesNotContain("checkouts", answer.GetRawText());
    }

    /// <summary>The list reads a real repository: a tree by its folder's name, never its path.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_list_names_each_tree_by_its_folder()
    {
        var root = await InitAsync();
        var module = await UpAsync(root);

        var answer = await AnswerAsync(module, "GIT_BRANCHES", new { all = true });

        var branches = answer.GetProperty("repositories")[0].GetProperty("branches").EnumerateArray().ToList();
        var main = branches.Single(branch => branch.GetProperty("name").GetString() == "main");
        Assert.Equal("engine", main.GetProperty("tree").GetString());
        Assert.False(main.TryGetProperty("worktree", out _));
        Assert.DoesNotContain("checkouts", answer.GetRawText());
    }

    /// <summary>One <c>GitReads</c> lives with the module, so a second ask of a commit is answered from its memory (D147 §4.2).</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_second_ask_is_remembered_by_the_module()
    {
        var root = await InitAsync();
        var commit = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var module = await UpAsync(root);

        var first = await AnswerAsync(module, "GIT_COMMIT", new { repository = "engine", commit });
        var second = await AnswerAsync(module, "GIT_COMMIT", new { repository = "engine", commit });

        Assert.Equal(JsonValueKind.Null, first.GetProperty("refusal").ValueKind);
        Assert.False(first.GetProperty("remembered").GetBoolean());
        Assert.True(second.GetProperty("remembered").GetBoolean());
        Assert.Equal("base", second.GetProperty("value").GetProperty("subject").GetString());
        Assert.NotEmpty(second.GetProperty("commands").EnumerateArray());
    }

    /// <summary>GITAPP2c: before the driver is up, and for a repository with no checkout here, each ref act is the cold start or a coded answer.</summary>
    [Theory]
    [InlineData("GIT_REF_CREATE")]
    [InlineData("GIT_REF_UNDO")]
    [InlineData("GIT_REF_DELETE")]
    [InlineData("GIT_REF_RENAME")]
    [InlineData("GIT_REF_FINISH")]
    public async Task A_ref_act_before_the_driver_is_up_is_the_cold_start_sentence(string route) =>
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), route, new { repository = "engine", kind = "branch", name = "x", to = "y", act = "a" }));

    [Fact]
    public async Task A_ref_act_of_a_repository_with_no_checkout_is_refused_as_data_with_its_code()
    {
        var module = await UpAsync(null);

        var answer = await AnswerAsync(module, "GIT_REF_CREATE", new { repository = "engine", kind = "branch", name = "topic" });

        Assert.True(answer.GetProperty("refused").GetBoolean());
        Assert.Equal(Refusals.GitNoCheckout, answer.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_create_names_its_kind_and_its_name()
    {
        var module = await UpAsync(Checkout("engine", git: true));

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "GIT_REF_CREATE", new { repository = "engine", name = "topic" }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "GIT_REF_CREATE", new { repository = "engine", kind = "branch" }));
    }

    /// <summary>
    /// The page's create runs the act in the journal and answers it: done, its act, no machine path; a name that stands is refused as data and
    /// nothing is written; the undo deletes at the id the act made.
    /// </summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_create_is_journaled_and_answered_and_its_undo_deletes_at_the_id_it_made()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await InitAsync();
        var module = await UpAsync(root);

        var made = await AnswerAsync(module, "GIT_REF_CREATE", new { repository = "engine", kind = "branch", name = "topic" });
        var again = await AnswerAsync(module, "GIT_REF_CREATE", new { repository = "engine", kind = "branch", name = "topic" });

        Assert.Equal("done", made.GetProperty("outcome").GetString());
        Assert.False(made.GetProperty("refused").GetBoolean());
        Assert.Equal((await GitAsync(root, "rev-parse", "HEAD")).Trim(), (await GitAsync(root, "rev-parse", "refs/heads/topic")).Trim());
        Assert.DoesNotContain("checkouts", made.GetRawText());
        Assert.True(again.GetProperty("refused").GetBoolean());
        Assert.Contains("already", again.GetProperty("said").GetString());

        var undone = await AnswerAsync(module, "GIT_REF_UNDO", new { repository = "engine", act = made.GetProperty("act").GetString() });

        Assert.Equal("done", undone.GetProperty("outcome").GetString());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "topic")).Trim());
    }

    /// <summary>
    /// GITAPP2d: the page's delete runs the act in the journal and answers it, the line it reads refusing the line itself and a commit that is
    /// not the one shown; the undo makes the branch again.
    /// </summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_delete_is_journaled_and_answered_and_its_undo_makes_the_branch_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await InitAsync();
        await GitAsync(root, "branch", "topic");
        var tip = (await GitAsync(root, "rev-parse", "topic")).Trim();
        var module = await UpAsync(root);

        var stale = await AnswerAsync(module, "GIT_REF_DELETE", new { repository = "engine", kind = "branch", name = "topic", at = new string('0', 40) });
        var line = await AnswerAsync(module, "GIT_REF_DELETE", new { repository = "engine", kind = "branch", name = "main", at = tip });
        var deleted = await AnswerAsync(module, "GIT_REF_DELETE", new { repository = "engine", kind = "branch", name = "topic", at = tip });

        Assert.True(stale.GetProperty("refused").GetBoolean());
        Assert.True(line.GetProperty("refused").GetBoolean());
        Assert.Equal("done", deleted.GetProperty("outcome").GetString());
        Assert.DoesNotContain("checkouts", deleted.GetRawText());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "topic")).Trim());
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "refs/daoris/discarded/topic")).Trim());

        var undone = await AnswerAsync(module, "GIT_REF_UNDO", new { repository = "engine", act = deleted.GetProperty("act").GetString() });

        Assert.Equal("done", undone.GetProperty("outcome").GetString());
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "refs/heads/topic")).Trim());
    }

    /// <summary>
    /// GITAPP2e: the page's rename runs the act in the journal and answers it, refusing the line and a taken name as data; the undo moves the branch
    /// back, and Finish is refused for an act that did not stop partway.
    /// </summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_rename_is_journaled_and_answered_and_its_undo_moves_the_branch_back()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await InitAsync();
        await GitAsync(root, "branch", "topic");
        await GitAsync(root, "branch", "taken");
        var tip = (await GitAsync(root, "rev-parse", "topic")).Trim();
        var module = await UpAsync(root);

        var line = await AnswerAsync(module, "GIT_REF_RENAME", new { repository = "engine", kind = "branch", name = "main", to = "trunk", at = tip });
        var taken = await AnswerAsync(module, "GIT_REF_RENAME", new { repository = "engine", kind = "branch", name = "topic", to = "taken", at = tip });
        var renamed = await AnswerAsync(module, "GIT_REF_RENAME", new { repository = "engine", kind = "branch", name = "topic", to = "work", at = tip });

        Assert.True(line.GetProperty("refused").GetBoolean());
        Assert.True(taken.GetProperty("refused").GetBoolean());
        Assert.Equal("done", renamed.GetProperty("outcome").GetString());
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "refs/heads/work")).Trim());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "topic")).Trim());

        var finish = await AnswerAsync(module, "GIT_REF_FINISH", new { repository = "engine", act = renamed.GetProperty("act").GetString() });
        var undone = await AnswerAsync(module, "GIT_REF_UNDO", new { repository = "engine", act = renamed.GetProperty("act").GetString() });

        Assert.True(finish.GetProperty("refused").GetBoolean());
        Assert.Equal("done", undone.GetProperty("outcome").GetString());
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "refs/heads/topic")).Trim());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "work")).Trim());
    }

    /// <summary>A terminal's press, asked for its words (<c>said</c>), is the verb's own lines and exit code, the act run as the terminal's.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_terminals_create_answers_the_verbs_lines_and_exit_code()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await InitAsync();
        var module = await UpAsync(root);

        var made = await AnswerAsync(module, "GIT_REF_CREATE", new { repository = "engine", kind = "tag", name = "v1", said = true });
        var refused = await AnswerAsync(module, "GIT_REF_CREATE", new { repository = "engine", kind = "tag", name = "v1", said = true });

        Assert.Equal(0, made.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Contains("created the tag `v1`", made.GetProperty("said").GetProperty("out")[0].GetString());
        Assert.Equal(1, refused.GetProperty("said").GetProperty("code").GetInt32());
    }

    private async Task<string> InitAsync()
    {
        var root = Checkout("engine");
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "Fixture\n");
        await GitAsync(root, "add", "README.md");
        await GitAsync(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", "base");
        return root;
    }

    private static async Task<string> GitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error);
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }

    /// <summary>The service: the registry with <c>engine</c>'s checkout at <paramref name="root"/> (none where null), and no sessions.</summary>
    private sealed class StandIn(string? root) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            object body = request.RequestUri!.AbsolutePath == "/api/registry" && root is not null
                ? new object[] { new { repository = "engine", workspace = "aurora", root } }
                : Array.Empty<object>();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            });
        }
    }
}
