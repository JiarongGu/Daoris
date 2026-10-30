using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1q (D212; the foundation design §2.3, §7 row 9; the survey's §1.3 row 6): <i>may this session ask for a helper?</i>, asked
/// of the start layer, the driver's judge and the terminal's <c>sessions helper</c> over one table, <c>fixtures/helper-askers.json</c>
/// among the service's tests, which the page's route and the service's desk and connector are held to as well, row for row.
/// </summary>
public sealed class HelperAskersTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-30T10:00:00Z");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-helper-askers-" + Guid.NewGuid().ToString("N")[..8]);

    public HelperAskersTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static TheoryData<string> Rows => [.. Table.Read().Rows.Select(row => row.Name)];

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task The_start_layer_the_judge_and_the_terminal_answer_each_asker_alike(string name)
    {
        var table = Table.Read();
        var row = table.Rows.Single(each => each.Name == name);
        var marks = row.Marks;

        Assert.Equal(row.Refused, StartAdmission.HelperAsker(new WorkMarks(false, marks.Kind, marks.Repository, marks.Ask, marks.Serves, marks.Piece, marks.Opinion)));

        var asker = new PriorSession("s1", "trees/default/engine/s-1", "completed", Repository: marks.Repository)
        {
            Kind = marks.Kind, Ask = marks.Ask, Serves = marks.Serves, Piece = marks.Piece, Opinion = marks.Opinion,
        };
        var judged = HelperJudge.Judge(new HelperAsk("a1b2c3d4", "s1", "Why?", null, HelperAsk.ByAgent, At), null, asker, new HelperRule(HelperRules.On, []), 0, true);
        Assert.Equal(row.Refused is null ? HelperVerdict.Go : HelperVerdict.Refuse(HelperWhy.AskerUnfit), judged);

        var host = new OpinionStandIn().Session("s1", "completed", tree: "trees/default/engine/s-1", kind: marks.Kind);
        host.Set("s1", "repository", marks.Repository);
        foreach (var (field, value) in new[] { ("ask", marks.Ask), ("serves", marks.Serves), ("piece", marks.Piece), ("opinion", marks.Opinion) })
        {
            if (value is not null) host.Set("s1", field, value);
        }

        var output = new StringWriter();
        var ask = SessionsCommand.Read(["helper", "s1", "Why?"], out _)!;
        var exit = await SessionsCommand.RunAsync(ask, new SessionsWorld(host.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null), output);

        Assert.Equal(row.Refused is null ? 0 : 1, exit);
        if (row.Refused is { } refused) Assert.Contains(table.Says[refused], output.ToString());
        Assert.Equal(row.Refused is null ? 1 : 0, new HelperFiles(_home).Asks().Count);
    }

    private sealed record Marks(string Kind, string Repository, string? Ask, string? Serves, string? Piece, string? Opinion);

    private sealed record Row(string Name, Marks Marks, string? Refused);

    private sealed record Table(IReadOnlyDictionary<string, string> Says, IReadOnlyList<Row> Rows)
    {
        public static Table Read() =>
            JsonSerializer.Deserialize<Table>(
                File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "helper-askers.json")),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
