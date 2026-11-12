using System.Text;
using System.Text.Json.Nodes;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The doctrine tool's own <c>status --json</c>, run over a fixture adopter, read through
/// <c>RECONCILE_PLAN</c>. The adopter is the example engine as this checkout's canon synced it, copied, then moved: its lock written from
/// another canon at the same version and one file it records left out, and a keep judged against an older canon text. The CLI is this
/// checkout's, on the <c>node</c> the PATH finds, as an install's program hands it the doctrine commands.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class ReconcileStatusProcessTests : DriverModuleBridge
{
    private readonly SetupStandIn _world;

    private readonly string _adopter;

    public ReconcileStatusProcessTests() : base("reconcile-status")
    {
        _adopter = Path.Combine(Home, "engine");
        Copy(Path.Combine(GitSeed.RepoRoot(), "examples", "engine"), _adopter);
        var node = CommandPresence.Resolve("node", startable: true) ?? throw new InvalidOperationException("no `node` on the PATH");
        var cli = Path.Combine(GitSeed.RepoRoot(), "src", "Daoris.Cli", "bin", "daoris.mjs");
        _world = new SetupStandIn
        {
            Reads = (row, ct) => DoctrineStatusRead.ReadAsync(node, [cli], row.Root!, Tools.Read(Home), Home, ct),
        };
        _world.Registry.Clear();
        _world.Registry.Add(new RepoView("engine", true, _adopter, "work"));
        _world.Facts = _world.Facts with { Adopted = true, Locked = true };
    }

    [Fact]
    public async Task An_adopter_synced_from_this_canon_offers_nothing()
    {
        var plan = await PlanAsync();

        Assert.False(plan.GetProperty("offered").GetBoolean(), plan.ToString());
        var status = plan.GetProperty("status");
        Assert.True(status.GetProperty("synced").GetBoolean());
        Assert.True(DriverModuleReconcileTests.Absent(status, "update"));
        Assert.Equal(0, status.GetProperty("reopened").GetInt32());
    }

    [Fact]
    public async Task An_adopter_whose_lock_is_another_canons_and_whose_keep_was_judged_before_it_moved_is_offered()
    {
        var locked = Json("daoris.lock");
        locked["canonDigest"] = new string('0', 64);
        var entries = locked["entries"]!.AsArray();
        var left = entries.First(each => each!["target"]!.GetValue<string>() == "knowledge/autonomous-development.md");
        entries.Remove(left);
        Write("daoris.lock", locked);
        var manifest = Json("daoris.json");
        manifest["kept"] = new JsonObject
        {
            ["knowledge/claims-need-checks.md"] = new JsonObject { ["reason"] = "this repository's own wording", ["canon"] = new string('1', 64) },
        };
        Write("daoris.json", manifest);

        var plan = await PlanAsync();

        Assert.True(plan.GetProperty("offered").GetBoolean(), plan.ToString());
        var status = plan.GetProperty("status");
        Assert.Equal(DoctrineUpdates.Differs, status.GetProperty("update").GetString());
        Assert.Equal(status.GetProperty("carried").GetString(), status.GetProperty("locked").GetString());
        Assert.Equal((0, 1, 0, 1), (status.GetProperty("changed").GetInt32(), status.GetProperty("added").GetInt32(), status.GetProperty("retired").GetInt32(), status.GetProperty("reopened").GetInt32()));
        Assert.True(plan.GetProperty("pressable").GetBoolean(), plan.ToString());
    }

    [Fact]
    public async Task An_adopter_whose_lock_an_older_canon_wrote_is_behind_and_one_a_newer_wrote_is_refused()
    {
        var locked = Json("daoris.lock");
        locked["canonVersion"] = "0.0.0";
        Write("daoris.lock", locked);

        var behind = await PlanAsync();
        Assert.True(behind.GetProperty("offered").GetBoolean(), behind.ToString());
        Assert.Equal(DoctrineUpdates.Behind, behind.GetProperty("status").GetProperty("update").GetString());
        Assert.Equal("0.0.0", behind.GetProperty("status").GetProperty("locked").GetString());

        locked["canonVersion"] = "9.9.9";
        Write("daoris.lock", locked);

        var newer = await PlanAsync();
        Assert.False(newer.GetProperty("offered").GetBoolean(), newer.ToString());
        Assert.Equal("9.9.9", newer.GetProperty("status").GetProperty("newerLock").GetString());
        Assert.Contains(newer.GetProperty("refusals").EnumerateArray(), each => each.GetProperty("code").GetString() == SetupRefusals.NewerLock);
    }

    private async Task<System.Text.Json.JsonElement> PlanAsync()
    {
        (StaffedRoles.Config("claude-code-acp")).WithDrivable("engine", true).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop) { SetupWorlds = _ => _world, Patience = TimeSpan.FromMinutes(2) };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SetupStandIn.EmptyHost())));
        return await AnswerAsync(module, "RECONCILE_PLAN", new { repository = "engine" });
    }

    private JsonNode Json(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(_adopter, file)))!;

    private void Write(string file, JsonNode json) =>
        File.WriteAllText(Path.Combine(_adopter, file), json.ToJsonString(new() { WriteIndented = true }) + "\n", new UTF8Encoding(false));

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var folder in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(folder.Replace(from, to));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(from, to));
    }
}
