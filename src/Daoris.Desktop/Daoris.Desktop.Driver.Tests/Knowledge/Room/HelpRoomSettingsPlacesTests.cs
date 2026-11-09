using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The Settings places the driver's and the service's texts name are the ones the web's <c>domains.ts</c> declares, said as
/// the English catalogue names them: a text naming a domain the page does not have, or by a name it no longer carries, sends a
/// person to nothing.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class HelpRoomSettingsPlacesTests
{
    private static readonly string Root = Root_();

    private static string Root_()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    [GeneratedRegex(@"id: '(?<id>\w+)', label: '(?<label>settings\.domain\.\w+)'")]
    private static partial Regex DeclaredDomain();

    /// <summary>Every domain <c>domains.ts</c> declares, by id, with the English name its catalogue key gives it.</summary>
    private static IReadOnlyList<(string Id, string Name)> Declared()
    {
        var web = Path.Combine(Root, "src", "Daoris.Web", "src");
        var names = JsonDocument.Parse(File.ReadAllText(Path.Combine(web, "locales", "en", "settings.domain.json"))).RootElement;
        return DeclaredDomain().Matches(File.ReadAllText(Path.Combine(web, "settings", "domains.ts")))
            .Select(each => (each.Groups["id"].Value, names.GetProperty(each.Groups["label"].Value).GetString()!))
            .ToList();
    }

    /// <summary>
    /// The places a go may name are the page's domains, by the page's names, in its order — so the room's list cannot name
    /// one the page lacks, or by a name the page's list does not show. Tools alone is not a place a go names yet, as the web
    /// twin's own test holds (<c>help/places.test.ts</c>); a text may still say it.
    /// </summary>
    [Fact]
    public void The_domains_a_go_may_name_are_the_ones_the_page_declares()
    {
        var declared = Declared().Where(each => each.Id != "tools").ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, HelpPlaces.Domains.ToList());
    }

    /// <summary>
    /// Each text of the driver and the service that says <c>Settings → X</c>, and each help room's golden, names an
    /// <c>X</c> the page declares, and none nests one domain in another. A place that left Settings (Workspace, Permissions, Plugins, Agents) is said by the place
    /// it went to, never as a Settings domain.
    /// </summary>
    [Fact]
    public void A_text_naming_a_Settings_domain_names_one_the_page_declares()
    {
        var names = Declared().Select(each => Regex.Escape(each.Name));
        var named = new Regex($"^(?:{string.Join('|', names)})(?![A-Za-z])");
        // A domain is a place of its own, never a part of another: "Settings → This Daoris → Disk space" names one inside one.
        var nested = new Regex($"^(?:{string.Join('|', names)}) → (?:{string.Join('|', names)})(?![A-Za-z])");
        var wrong = new List<string>();

        foreach (var file in Texts())
        {
            var text = File.ReadAllText(file);
            for (var at = text.IndexOf("Settings → ", StringComparison.Ordinal); at >= 0;
                at = text.IndexOf("Settings → ", at + 1, StringComparison.Ordinal))
            {
                var rest = text[(at + "Settings → ".Length)..];
                if (!named.IsMatch(rest) || nested.IsMatch(rest)) wrong.Add($"{Path.GetRelativePath(Root, file)}: Settings → {rest[..Math.Min(rest.Length, 30)].Split('\n')[0]}");
            }
        }

        Assert.Empty(wrong);
    }

    /// <summary>The desktop's projects by name, the service's own code and the help room's goldens: not a test, which says what a text must not.</summary>
    private static IEnumerable<string> Texts()
    {
        IEnumerable<string> desktop =
        [
            .. SourceFiles.All("Daoris.Desktop.App", "*.cs"), .. SourceFiles.All(SourceFiles.DriverProject, "*.cs"),
            .. SourceFiles.All("Daoris.Desktop.Driver.Host", "*.cs"), .. SourceFiles.All("Daoris.Desktop.GitDoor", "*.cs"),
            .. SourceFiles.All("Daoris.Desktop.Launcher", "*.cs"), .. SourceFiles.All("Daoris.Desktop.Modules", "*.cs"),
            .. SourceFiles.All("Daoris.Desktop.OwnerStandIn", "*.cs"), .. SourceFiles.All("Daoris.Desktop.PieceStart", "*.cs"),
        ];
        foreach (var file in desktop) yield return file;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "src", "Daoris.Service"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/") || relative.Contains(".Tests/")) continue;
            yield return file;
        }
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(HelpRoomGoldenTests.GoldenPath("full.md"))!, "*.md")) yield return file;
    }
}
