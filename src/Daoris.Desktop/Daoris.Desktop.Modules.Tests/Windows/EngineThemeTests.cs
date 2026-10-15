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
/// The kit's setting, through the kit's route: <c>UseWindows</c> registers the app's <see cref="IColorScheme"/>, the
/// engine applies it to the Chromium every window shares, and each window's page sets it to the viewer's choice over
/// <c>SHENORA.COLOR_SCHEME</c>, which <c>AddShenoraColorScheme</c> mounts. Why the choice and not <c>SET_THEME</c>'s
/// flag: D22's THEMESCHEME1 note.
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
