using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row f, s-ix f4: the pin's install is one decision the window's actions and the terminal's <c>agent pin</c> ask
/// (<see cref="PinInstall.Plan"/>): a channel's version refused before anything is fetched, an install in place fetching nothing, a
/// package's command named with its prefix, and a program declaring neither refused with one sentence.
/// </summary>
public sealed class PinInstallTests : IDisposable
{
    private readonly string _home = ConformanceScratch.New("pin-install");

    public void Dispose() => ConformanceScratch.Remove(_home);

    private static HarnessToolchain Toolchain(string door) => AdapterSet.Built().Resolve(door).Toolchain!;

    [Fact]
    public void A_channel_program_not_yet_installed_is_staged_from_its_channel()
    {
        var toolchain = Toolchain("claude-code");

        var plan = PinInstall.Plan(toolchain, _home, "claude-code", "2.1.287", ["stand-in-npm"]);

        Assert.Equal(PinWay.Channel, plan.Way);
        Assert.Equal(toolchain.Channel, plan.Channel);
        Assert.Equal(HarnessSettings.ManagedHome(_home, "claude-code", "2.1.287"), plan.Where);
        Assert.Null(plan.Command);
    }

    [Fact]
    public void A_version_the_channel_cannot_verify_is_refused_before_anything_is_fetched_and_never_handed_to_npm()
    {
        Assert.Throws<DriverException>(() => PinInstall.Plan(Toolchain("claude-code"), _home, "claude-code", "latest", ["stand-in-npm"]));
    }

    [Fact]
    public void A_package_program_is_installed_by_the_package_managers_command_under_the_folder_daoris_keeps_it_in()
    {
        var toolchain = Toolchain("claude-code-acp");
        Assert.True(toolchain.Channel is not { Length: > 0 } && toolchain.Package is { Length: > 0 });

        var plan = PinInstall.Plan(toolchain, _home, "claude-code-acp", "1.2.3", ["stand-in-npm"]);

        Assert.Equal(PinWay.Package, plan.Way);
        Assert.Equal(["stand-in-npm", "install", "--prefix", plan.Where, $"{toolchain.Package}@1.2.3"], plan.Command);
    }

    [Fact]
    public void A_program_declaring_no_package_and_no_channel_is_refused_with_the_one_sentence()
    {
        var refused = Assert.Throws<DriverException>(() => PinInstall.Plan(Toolchain("stub"), _home, "stub", "1.0.0", ["stand-in-npm"]));

        Assert.StartsWith("that agent declares no package", refused.Message);
    }
}
