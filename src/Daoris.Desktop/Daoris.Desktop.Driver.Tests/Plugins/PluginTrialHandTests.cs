using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>`plugin try` asks the question the desktop's start asks: the token of the identity serving origin, through <see cref="PluginHostToken"/>.</summary>
public sealed class PluginTrialHandTests : IDisposable
{
    private const string Host = "git.example.invalid";
    private const string Origin = "https://git.example.invalid/acme/app.git";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-trial-hand-" + Guid.NewGuid().ToString("N")[..8], "data");

    public void Dispose() => GitSeed.Discard(Directory.GetParent(_home)!.FullName);

    private void Install(bool takesToken)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "pr");
        Directory.CreateDirectory(folder);
        var steps = takesToken ? "[{ \"id\": \"pull-request\", \"reach\": \"performed\" }]" : "[]";
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            "{ \"id\": \"pr\", \"hooks\": { \"command\": [\"no-such-tool-for-trials\"], \"points\": [\"work/watch\", \"work/step\", \"work/perform\"] }, \"steps\": " + steps + " }");
    }

    private void Serve(bool apiToken)
    {
        var identity = GitIdentityHttps.AddHttps(_home, Host, "Work", "alice", null, null, "throwaway-https-token");
        if (apiToken) GitIdentityTokens.Set(_home, identity.Id, GitTokens.Api, "throwaway-api-token");
    }

    [Fact]
    public async Task A_plugin_whose_start_would_be_refused_for_want_of_a_token_fails_try_in_plain_words()
    {
        Install(takesToken: true);
        Serve(apiToken: false);

        var trial = await PluginTrials.TryInstalledAsync(_home, "pr", new TrialOptions(Origin: Origin));

        var step = Assert.Single(trial.Steps, s => s.Name == "host-token");
        Assert.False(step.Ok);
        Assert.Contains("this plugin's start needs an API token on identity Work; set it in Settings → Git", step.Sentence);
    }

    [Fact]
    public async Task A_token_set_passes_the_step_and_a_plugin_that_takes_none_has_no_step()
    {
        Install(takesToken: true);
        Serve(apiToken: true);
        var held = await PluginTrials.TryInstalledAsync(_home, "pr", new TrialOptions(Origin: Origin));
        Assert.True(Assert.Single(held.Steps, s => s.Name == "host-token").Ok);

        Install(takesToken: false);
        var none = await PluginTrials.TryInstalledAsync(_home, "pr", new TrialOptions(Origin: Origin));
        Assert.DoesNotContain(none.Steps, s => s.Name == "host-token");
    }
}
