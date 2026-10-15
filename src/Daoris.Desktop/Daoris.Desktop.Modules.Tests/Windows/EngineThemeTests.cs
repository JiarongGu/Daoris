using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;
using Shenora.Core.Shell;
using Shenora.Modules.Platform;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// THEMESCHEME1: the engine every window renders on follows the viewer's chosen theme, so Chromium's own UI and the
/// page's <c>prefers-color-scheme</c> agree with the page on a desktop set the other way.
/// </summary>
/// <remarks>
/// <para><b>The kit's setting, through the kit's route.</b> <c>UseWindows</c> registers the app's
/// <see cref="IColorScheme"/>, and the engine applies it to Chromium's global request context, which every window's
/// browser takes (the main window's and each secondary window's alike: none is made with a context of its own). The page
/// sets it by its choice over <c>SHENORA.COLOR_SCHEME</c>, which <c>AddShenoraColorScheme</c> mounts.</para>
///
/// <para><b>The choice, never the effective theme.</b> <c>SET_THEME</c>'s one flag cannot tell *System* from a held
/// theme, and a scheme held at the OS's present value would leave a viewer on System behind the OS's next turn, since the
/// page reads the OS through the very media query the held scheme answers.</para>
/// </remarks>
public sealed class EngineThemeTests : Bridge
{
    /// <summary>
    /// Each of the page's choices (<c>ThemeChoice</c> in <c>design/theme.ts</c>) is a setting the kit's route takes, by
    /// the name the page sends: the twin of the page's call, held against the kit's own module rather than a stand-in.
    /// </summary>
    [Theory]
    [InlineData("system", ColorScheme.System)]
    [InlineData("light", ColorScheme.Light)]
    [InlineData("dark", ColorScheme.Dark)]
    public async Task The_kits_route_takes_each_choice_the_page_sends(string choice, ColorScheme setting)
    {
        // Started away from the choice, so a route that took nothing is seen.
        var scheme = new ColorSchemeState(setting == ColorScheme.Dark ? ColorScheme.Light : ColorScheme.Dark);
        using var services = new ServiceCollection()
            .AddSingleton<IColorScheme>(scheme)
            .AddShenoraColorScheme()
            .BuildServiceProvider();
        var route = services.GetServices<IIpcModule>().Single(module => module.ModuleName == ColorSchemeModule.Module);

        await AnswerAsync(route, ColorSchemeModule.SetSchemeType, new { scheme = choice });

        Assert.Equal(setting, scheme.Scheme);
    }

    /// <summary>
    /// Held on the application's own source, as the browser's start is (CHR8), because the window's composition has no
    /// test project: the route is mounted, and the handshake advertises it, as the kit asks of an app that mounts it.
    /// </summary>
    [Fact]
    public void The_application_mounts_the_kits_route_and_advertises_it()
    {
        var program = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Program.cs"));

        Assert.Contains("builder.Services.AddShenoraColorScheme();", program);
        var shell = program[program.IndexOf("Shell = new Shenora.Core.Ipc.ShellInfo", StringComparison.Ordinal)..];
        Assert.Contains("Capabilities = [ShellCapability.ColorScheme]", shell[..shell.IndexOf('}')]);
    }
}
