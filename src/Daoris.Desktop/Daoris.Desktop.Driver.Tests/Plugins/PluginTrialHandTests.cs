using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>`plugin try` asks the question the desktop's start asks: whether the place's origin may be written, through <see cref="PlaceHands"/>.</summary>
public sealed class PluginTrialHandTests : IDisposable
{
    private const string Host = "git.example.invalid";
    private const string Origin = "https://git.example.invalid/acme/app.git";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-trial-hand-" + Guid.NewGuid().ToString("N")[..8], "data");

    public void Dispose() => GitSeed.Discard(Directory.GetParent(_home)!.FullName);

    private void Install(bool placed)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "pr");
        Directory.CreateDirectory(folder);
        var steps = placed ? "[{ \"id\": \"pull-request\", \"reach\": \"performed\" }]" : "[]";
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            "{ \"id\": \"pr\", \"hooks\": { \"command\": [\"no-such-tool-for-trials\"], \"points\": [\"work/watch\", \"work/step\", \"work/perform\"] }, \"steps\": " + steps + " }");
    }

    [Fact]
    public async Task A_plugin_whose_start_would_be_refused_for_a_read_only_identity_fails_try_in_plain_words()
    {
        Install(placed: true);
        var identity = GitIdentityHttps.AddHttps(_home, Host, "Work", "alice", null, null, "throwaway-https-token");
        GitIdentityActs.ReadOnly(_home, identity.Id, on: true);

        var trial = await PluginTrials.TryInstalledAsync(_home, "pr", new TrialOptions(Origin: Origin));

        var step = Assert.Single(trial.Steps, s => s.Name == PluginTrialHand.Name);
        Assert.False(step.Ok);
        Assert.Contains("Work is read only on git.example.invalid", step.Sentence);
    }

    [Fact]
    public async Task No_identity_passes_the_step_and_a_plugin_with_no_place_has_no_step()
    {
        Install(placed: true);
        var held = await PluginTrials.TryInstalledAsync(_home, "pr", new TrialOptions(Origin: Origin));
        Assert.True(Assert.Single(held.Steps, s => s.Name == PluginTrialHand.Name).Ok);

        Install(placed: false);
        var none = await PluginTrials.TryInstalledAsync(_home, "pr", new TrialOptions(Origin: Origin));
        Assert.DoesNotContain(none.Steps, s => s.Name == PluginTrialHand.Name);
    }
}
