using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings cast [&lt;workspace&gt;|machine] [--repository &lt;name&gt;] [&lt;role&gt; &lt;holder&gt;|default] [--why "…"]</c>: the terminal twin of a
/// place's cast (<see cref="PlaceCastFiles"/>), and with no role its read form (<see cref="PlaceCastView"/>). The owner's record, as the place's
/// risk is: a process in an agents' job is refused, and an agent's press goes on the owner's card.
/// </summary>
public sealed class PlaceCastVerb : ChoiceVerb
{
    private const string Usage = "usage: daoris settings cast [<workspace>|machine] [--repository <name>] [<role> person|agent|agent:<name>|default] [--why \"…\"] — "
        + "e.g. `daoris settings cast aurora acceptor agent:codex --why \"low-risk docs\"`; with no role it says each role's holder now and why.";

    private static readonly string[] Valued = ["--repository", "--why"];

    public override string Word => "cast";

    /// <summary>Whether the words cast anything: a role and a holder after the place.</summary>
    public static bool Changes(IReadOnlyList<string> args) => Read(args).Role is not null;

    public override int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var asked = Read(args);
        var home = DriverConfig.HomeOf(world.ConfigPath);
        var read = new PlaceCastFiles(home).Read();
        var view = PlaceCastView.Of(read.Book, before.PlaceRisk, asked.Workspace, asked.Repository, ProviderRegistry.Built().Agents,
            RoleCatalog.ReadHome(home).Slots, read.Problem);
        foreach (var line in view.Said()) world.Instance.Out.WriteLine(line);
        return 0;
    }

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var asked = Read(args);
        var home = DriverConfig.HomeOf(world.ConfigPath);
        var agent = world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 } || world.InAgentJob() == true;
        var payload = new JsonObject { ["role"] = asked.Role, ["holder"] = asked.Holder, ["why"] = asked.Why };
        if (asked.Workspace is not null) payload["workspace"] = asked.Workspace;
        if (asked.Repository is not null) payload["repository"] = asked.Repository;
        return new("cast", ChoiceRoutes.PlaceCast, payload, _ =>
        {
            if (agent) throw new ChoiceWords(PlaceRiskVerb.Alone, 1);
            var files = new PlaceCastFiles(home);
            if (files.Cast(PlaceRiskBook.KeyOf(asked.Workspace, asked.Repository), asked.Role!, asked.Holder, asked.Why, world.Now()) is { } refused)
            {
                throw new ChoiceWords($"the cast was refused ({refused}{(refused == PlaceCastFiles.Unreadable ? $": {files.Problem()}" : "")}) — {Usage}", 1);
            }

            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            world.Instance.Out.WriteLine($"daoris: {PlaceRiskBook.KeyOf(asked.Workspace, asked.Repository)}: `{asked.Role}` is cast to {(asked.Holder is { } holder ? CastHolders.Said(holder) : "its default")}.");
            return 0;
        });
    }

    private sealed record Asked(string? Workspace, string? Repository, string? Role, string? Holder, string? Why);

    private static Asked Read(IReadOnlyList<string> args)
    {
        try
        {
            Flags(args, Valued, [], "settings cast", Usage);
            var operands = Operands(args, Valued).Skip(1).ToList();
            // The place first where three words are given, or one that is no role; then the role and its holder.
            var place = operands.Count is 1 or 3 ? operands[0] : null;
            var cast = place is null ? operands : operands.Skip(1).ToList();
            if (cast.Count is not (0 or 2)) throw new ChoiceWords("`settings cast` takes a place, then a role and whom it is cast to.");
            var workspace = place is not null && place != PlaceRiskBook.MachineKey ? place : null;
            var repository = FlagValue(args, "--repository");
            if (repository is not null && workspace is null) throw new ChoiceWords("`settings cast --repository` needs the repository's workspace.");
            if (cast.Count == 2 && cast[1] != "default" && !CastHolders.Is(cast[1])) throw new ChoiceWords($"`{cast[1]}` is no holder: `person`, `agent`, `agent:<name>` or `default`.");
            return new Asked(workspace, repository, cast.Count == 2 ? cast[0] : null, cast.Count == 2 && cast[1] != "default" ? cast[1] : null, FlagValue(args, "--why"));
        }
        catch (ChoiceWords refused) when (!refused.Message.EndsWith(Usage, StringComparison.Ordinal))
        {
            throw new ChoiceWords($"{refused.Message} — {Usage}", refused.Exit);
        }
    }
}
