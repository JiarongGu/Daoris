using System.Text.Json.Nodes;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// An end the service's sweep wrote names no session, so <i>What needs you</i> keeps it as a row of its own, by its operation: the
/// repository as where, the entry it ends, the revision it read and its reason, with no session for a door to open. A session's memory row
/// beside it is still by its session.
/// </summary>
public sealed class AttentionMemoryEndRowTests
{
    private const string Op = "9f8e7d6c5b4a3921";

    private static JsonObject Wait(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>🔴 The sweep's end is a row by its operation, whole, with no session; the session's row is by its session.</summary>
    [Fact]
    public void A_sweep_s_end_is_a_row_by_its_operation_with_no_session()
    {
        var rows = Rows(new()
        {
            Sessions = [Session("""{"id":"c0nv0001","quest":"7a82cc","state":"completed"}""")],
            Registry = [Registration("engine")],
            Memory =
            [
                Wait("""{"session":"c0nv0001","ends":1,"replacements":0,"staged":0,"since":"2026-10-31T09:00:00+00:00"}"""),
                Wait("""{"ends":1,"replacements":0,"staged":0,"since":"2026-10-31T10:00:00+00:00","end":{"op":"9f8e7d6c5b4a3921","repository":"engine","target":"m42","revision":3,"reason":"Its cited record `r1` is superseded by `r2`."}}"""),
            ],
        });

        Assert.Equal([$"memory:{Op}", "memory:c0nv0001"], Keys(rows));
        Is("""{"kind":"memory","id":"9f8e7d6c5b4a3921","since":"2026-10-31T10:00:00+00:00","where":"engine","memory":{"ends":1,"replacements":0,"staged":0,"end":{"op":"9f8e7d6c5b4a3921","repository":"engine","target":"m42","revision":3,"reason":"Its cited record `r1` is superseded by `r2`."}}}""",
            rows.Single(row => row["id"]!.GetValue<string>() == Op));
        Assert.Null(rows.Single(row => row["id"]!.GetValue<string>() == Op)["session"]);
    }
}
