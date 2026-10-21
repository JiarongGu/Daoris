using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPALL1, KEEPALL1b and KEEPALL1c (FIX-LOG 2026-10-17): every home file a door edits keeps what its writer does not own, as <c>driver.json</c>'s writers do
/// (<see cref="DriverConfigKeepsTests"/>). One table both twins read, <c>test/fixtures/home-files-keep.json</c>, row for row with the
/// CLI's <c>homefileskeep.test.ts</c>: an existing file in the writer's layout with keys neither twin knows at each level, one door's
/// edit, and the bytes both write. <c>browser/settings.json</c>'s writer is the modules', so its rows are
/// <c>BrowserSettingsTests</c>' there.
/// </summary>
public sealed class HomeFilesKeepTests
{
    [Theory]
    [MemberData(nameof(Rows))]
    public void An_edit_changes_only_what_it_edits(string name, string file, string edit, string start, string? between, string? refused, string expected)
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-keepall-" + Guid.NewGuid().ToString("N")[..8]);
        var path = Path.Combine(home, file);
        try
        {
            Directory.CreateDirectory(home);
            File.WriteAllText(path, start);
            var error = Record.Exception(() => Applied(home, file, JsonDocument.Parse(edit).RootElement, () =>
            {
                // Another writer's change, made after this one read the file and before it writes.
                if (between is not null) File.WriteAllText(path, between);
            }));
            if (refused is null) Assert.Null(error);
            else Assert.Contains(refused, Assert.IsType<DriverException>(error).Message);
            Assert.Equal((name, expected), (name, File.ReadAllText(path)));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>The edit through the door's own methods, as the window's pages, Ask Daoris and the host call them.</summary>
    private static void Applied(string home, string file, JsonElement edit, Action between)
    {
        string Text(string field) => edit.GetProperty(field).GetString()!;
        string? Name() => edit.TryGetProperty("name", out var name) ? name.GetString() : null;
        RuleScope Scope() => Text("scope") switch { "machine" => RuleScope.Machine, "workspace" => RuleScope.Workspace, _ => RuleScope.Repository };
        RuleList List() => Text("list") switch { "allow" => RuleList.Allow, "ask" => RuleList.Ask, _ => RuleList.Deny };

        switch (file, Text("op"))
        {
            case ("plugins.json", "plugin-disable"):
                between();
                PluginState.Disable(home, Text("id"));
                return;
            case ("plugins.json", "plugin-enable"):
                between();
                PluginState.Enable(home, Text("id"));
                return;
            case ("permissions.json", var op):
                var read = PermissionRules.Load(home);
                between();
                PermissionRules.Save(home, op switch
                {
                    "none" => read,
                    "permission-add" => PermissionRules.Add(read, Scope(), Name(), List(), Text("rule")),
                    "permission-remove" => PermissionRules.Remove(read, Scope(), Name(), Text("rule")),
                    "permission-default" => PermissionRules.SwitchDefault(read, Text("id"), edit.GetProperty("on").GetBoolean()),
                    _ => throw new InvalidOperationException($"the table names an edit this test does not make: {op}"),
                });
                return;
            case ("keys.json", "key-replace"):
                between();
                // An account the key replaces is one on this machine: its directory is there.
                Directory.CreateDirectory(HarnessSettings.ProfileHome(home, Text("agent"), Text("account")));
                HarnessKeys.Replace(home, Text("agent"), Text("account"), Text("key"));
                return;
            case ("keys.json", "key-add"):
                between();
                HarnessKeys.Add(home, Text("agent"), Text("key"));
                return;
            case ("plugin-approvals.json", "approval-approve"):
                between();
                // The plugin declares the server for Ask Daoris with the tools the edit names, and the person approves them whole.
                var folder = DaorisHome.Entry(home, HomeEntry.Plugins, Text("plugin"));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""
                    { "id": "{{Text("plugin")}}", "servers": [ { "name": "{{Text("server")}}", "command": ["{{Text("server")}}"], "to": ["help"],
                      "allow": {{edit.GetProperty("allow").GetRawText()}}, "writes": {{edit.GetProperty("writes").GetRawText()}} } ] }
                    """);
                HelpGrants.Approve(home, PluginCatalog.Load(home), Text("plugin"), Text("server"), readsOnly: false);
                return;
            case ("plugin-approvals.json", "approval-withdraw"):
                between();
                HelpGrants.Withdraw(home, Text("plugin"), Text("server"));
                return;
            case ("tools.json", "tool-system"):
                between();
                Tools.UseSystem(home, Text("id"));
                return;
            case ("tools.json", "location-add"):
                between();
                Tools.AddLocation(home, Text("address"));
                return;
            case ("tools.json", "location-remove"):
                between();
                Tools.RemoveLocation(home, Text("address"));
                return;
            case ("harnesses.json", var op):
                var path = Path.Combine(home, file);
                var wiring = HarnessSettings.Load(path);
                between();
                var workspace = edit.TryGetProperty("workspace", out var circle) ? circle.GetString() : null;
                (op switch
                {
                    "none" => wiring,
                    "harness-default" => workspace is null
                        ? wiring.WithDefault(Text("agent"), Text("account"))
                        : wiring.WithWorkspaceDefault(workspace, Text("agent"), Text("account")),
                    "harness-use" => wiring.WithUse(Text("agent"), new UseChange(Use: Text("use")), workspace),
                    "harness-pin" => wiring.WithVersion(Text("agent"), Text("version")),
                    "harness-order" => wiring.WithRotation(
                        Text("agent"), [.. edit.GetProperty("order").EnumerateArray().Select(name => name.GetString()!)], workspace),
                    _ => throw new InvalidOperationException($"the table names an edit this test does not make: {op}"),
                }).Save(path);
                return;
            default:
                throw new InvalidOperationException($"the table names an edit this test does not make: {file} {Text("op")}");
        }
    }

    private static readonly JsonDocument Table =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "home-files-keep.json")));

    private static string Lines(JsonElement lines) => string.Join("\n", lines.EnumerateArray().Select(line => line.GetString()));

    /// <summary><paramref name="file"/> with <paramref name="changes"/> made, its lines ended by CRLF where <paramref name="crlf"/> says.</summary>
    private static string Changed(string file, JsonElement? changes, bool crlf)
    {
        var text = Lines(Table.RootElement.GetProperty("files").GetProperty(file)) + "\n";
        foreach (var change in changes is { } made ? made.EnumerateArray().ToList() : [])
        {
            var from = Lines(change.GetProperty("from"));
            var at = text.IndexOf(from, StringComparison.Ordinal);
            if (at < 0 || text.IndexOf(from, at + 1, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException($"a change's `from` is in the file once: {from}");
            }

            var to = Lines(change.GetProperty("to"));
            // A change to nothing takes its line's end with it.
            text = to.Length == 0 && text[at + from.Length] == '\n' ? text[..at] + text[(at + from.Length + 1)..] : text[..at] + to + text[(at + from.Length)..];
        }

        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    public static TheoryData<string, string, string, string, string?, string?, string> Rows()
    {
        var rows = new TheoryData<string, string, string, string, string?, string?, string>();
        foreach (var row in Table.RootElement.GetProperty("rows").EnumerateArray())
        {
            // A row another twin alone writes is that twin's.
            if (row.TryGetProperty("only", out var only) && only.GetString() != "driver") continue;
            var file = row.GetProperty("file").GetString()!;
            if (file == "browser/settings.json") continue;
            var crlf = row.TryGetProperty("crlf", out var ends) && ends.GetBoolean();
            var start = Changed(file, row.TryGetProperty("start", out var first) ? first : null, crlf);
            var between = row.TryGetProperty("between", out var made) ? Changed(file, made, crlf) : null;
            var refused = row.TryGetProperty("refused", out var said) ? said.GetString() : null;
            rows.Add(row.GetProperty("name").GetString()!, file, row.GetProperty("edit").GetRawText(), start, between, refused,
                Changed(file, row.GetProperty("changes"), crlf));
        }

        return rows;
    }
}
