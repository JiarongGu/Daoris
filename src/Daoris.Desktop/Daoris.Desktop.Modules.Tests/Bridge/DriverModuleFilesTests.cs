using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A repository's files git does not manage, the window's: <c>FILES</c> reads the rule file's layers for the page, and the
/// person's rules and permissions are written by presses that are the window's alone. A press answers as data, a file that does not
/// read refuses every write and every allow, and no answer carries a machine path.
/// </summary>
public sealed class DriverModuleFilesTests : DriverModuleBridge
{
    private string FilesFile => DaorisHome.Entry(Home, HomeEntry.Files);

    [Fact]
    public async Task A_rule_set_and_an_allow_pressed_are_written_with_their_maker_and_taken_back_by_their_presses()
    {
        var module = Module();

        var set = await AnswerAsync(module, "FILES_SET", new { repository = "engine", kind = "local", paths = new[] { "local/" }, copy = true });
        var asked = await AnswerAsync(module, "FILES_PERMIT", new { repository = "engine", act = "add", list = "ask", rule = "run(npm *)" });
        var allowed = await AnswerAsync(module, "FILES_PERMIT", new { repository = "engine", act = "add", list = "allow", rule = "run(npm ci)" });

        Assert.All(new[] { set, asked, allowed }, answer => Assert.True(answer.GetProperty("done").GetBoolean()));
        var written = await File.ReadAllTextAsync(FilesFile);
        Assert.Contains("\"by\": \"person\"", written, StringComparison.Ordinal);
        Assert.Contains("\"reach\": \"copy\"", written, StringComparison.Ordinal);
        Assert.Contains("\"/local/\"", written, StringComparison.Ordinal);

        var taken = await AnswerAsync(module, "FILES_PERMIT", new { repository = "engine", act = "remove", list = "allow", rule = "run(npm ci)" });
        var removed = await AnswerAsync(module, "FILES_REMOVE", new { repository = "engine", paths = new[] { "local/" } });
        Assert.True(taken.GetProperty("done").GetBoolean());
        Assert.True(removed.GetProperty("done").GetBoolean());
        Assert.DoesNotContain("run(npm ci)", await File.ReadAllTextAsync(FilesFile), StringComparison.Ordinal);
        Assert.Equal("unchanged", (await AnswerAsync(module, "FILES_REMOVE", new { repository = "engine", paths = new[] { "local/" } })).GetProperty("why").GetString());
    }

    [Fact]
    public async Task A_file_that_does_not_read_refuses_every_write_and_every_allow_and_is_left_as_it_is()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilesFile)!);
        const string stray = """{ "machine": { "permissions": { "deny": ["run(*)"] } }, }""";
        await File.WriteAllTextAsync(FilesFile, stray);
        var module = Module();

        var allow = await AnswerAsync(module, "FILES_PERMIT", new { repository = "engine", act = "add", list = "allow", rule = "run(npm ci)" });
        var set = await AnswerAsync(module, "FILES_SET", new { repository = "engine", kind = "scratch", paths = new[] { "tmp/" } });

        Assert.Equal(("unread", "unread"), (allow.GetProperty("why").GetString(), set.GetProperty("why").GetString()));
        Assert.DoesNotContain(Home, allow.GetRawText() + set.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(stray, await File.ReadAllTextAsync(FilesFile));
    }

    [Fact]
    public async Task A_press_the_driver_refuses_is_answered_in_its_words()
    {
        var module = Module();

        var kind = await AnswerAsync(module, "FILES_SET", new { repository = "engine", kind = "folder", paths = new[] { "a/" } });
        var act = await AnswerAsync(module, "FILES_PERMIT", new { repository = "engine", act = "add", list = "allow", rule = "npm ci" });

        Assert.Equal(("refused", "refused"), (kind.GetProperty("why").GetString(), act.GetProperty("why").GetString()));
        Assert.False(File.Exists(FilesFile));
    }

    [Fact]
    public async Task A_repository_with_no_checkout_here_is_answered_with_its_code()
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(null))));

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "FILES_READ", new { repository = "engine" });

        Assert.Equal("no-checkout", answer.GetProperty("why").GetString());
    }

    /// <summary>The page's read answers each ignored top with its rule and layer, the person's rule over the found one beside it, and no path.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_files_read_answers_each_top_with_its_layers_and_no_machine_path()
    {
        var root = Path.Combine(Home, "checkouts", "engine");
        Directory.CreateDirectory(Path.Combine(root, "node_modules", "x"));
        await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "x", "i.js"), "1\n");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "node_modules/\n");
        await File.WriteAllTextAsync(Path.Combine(root, "package.json"), "{}\n");
        await File.WriteAllTextAsync(Path.Combine(root, "package-lock.json"), "{}\n");
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "add", ".gitignore", "package.json", "package-lock.json");
        await GitAsync(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", "base");
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(root))));
        var module = new DriverModule(Bus, loop);
        await AnswerAsync(module, "FILES_SET", new { repository = "engine", kind = "scratch", paths = new[] { "node_modules/" } });

        JsonElement answer = default;
        await UntilAsync(() =>
        {
            answer = AnswerAsync(module, "FILES", new { repository = "engine" }).GetAwaiter().GetResult();
            return answer.TryGetProperty("look", out _);
        });

        var file = answer.GetProperty("look").GetProperty("files").EnumerateArray().Single(each => each.GetProperty("path").GetString() == "node_modules/");
        Assert.Equal(("scratch", "person"), (file.GetProperty("kind").GetString(), file.GetProperty("layer").GetString()));
        Assert.Equal("dependency", file.GetProperty("below")[0].GetProperty("kind").GetString());
        Assert.DoesNotContain(Home, answer.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Home.Replace('\\', '/'), answer.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task GitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
    }

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
