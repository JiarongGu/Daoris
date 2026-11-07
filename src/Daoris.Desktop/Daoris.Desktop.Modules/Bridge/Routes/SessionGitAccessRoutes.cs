using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The press that tightens a session's git access: its id and the classes to narrow, each to <c>allow</c>, <c>ask</c> or <c>deny</c>.</summary>
public sealed record TightenSessionGitAccess(string Id, JsonElement? Access);

/// <summary>
/// A running session's git access, the page's half of <c>daoris git access --session</c>: what each session holds is in
/// the driver's state (<see cref="Wire"/>), and <c>TIGHTEN_SESSION_GIT_ACCESS</c> narrows it. It only narrows, so the route asks no
/// confirmation; a widening is a change of git access at its level, which the page confirms.
/// </summary>
public sealed class SessionGitAccessRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>The sessions' records as the driver's state carries them.</summary>
    public static object[] Wire(string home) =>
    [
        .. GitSessionAccess.All(home).Select(record => (object)new
        {
            record.Session,
            record.Repository,
            record.Workspace,
            Classes = record.Classes.Select(each => new { each.Class, each.Value, each.Source }).ToArray(),
        }),
    ];

    [DriverRoute("TIGHTEN_SESSION_GIT_ACCESS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("TIGHTEN_SESSION_GIT_ACCESS", Session = "id")]
    [GroundSession("TIGHTEN_SESSION_GIT_ACCESS", "id")]
    [Says("TIGHTEN_SESSION_GIT_ACCESS", "id:session")]
    [Publishes("TIGHTEN_SESSION_GIT_ACCESS", StateScopes.Driver, Session = "id")]
    private object? Tighten(TightenSessionGitAccess press)
    {
        var set = press.Access is { ValueKind: JsonValueKind.Object } access
            ? access.EnumerateObject().ToDictionary(each => each.Name, each => each.Value.ValueKind == JsonValueKind.String ? each.Value.GetString()! : "", StringComparer.Ordinal)
            : new Dictionary<string, string>();
        GitSessionAccess.Tighten(_loop.Home, press.Id, set);
        return DriverRoutes.State(_loop);
    }
}
