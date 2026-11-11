using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings terminal ask|act</c>: the tier of a person's press from a terminal no Daoris session owns
/// (<see cref="TerminalTiers"/>), itself a press of <c>SET_TERMINAL</c>, so setting <c>act</c> from a terminal is confirmed once on its card.
/// Acting alone, with no Daoris running, it writes <c>ask</c> and refuses <c>act</c>, which no card could grant.
/// </summary>
public sealed class TerminalVerb : ChoiceVerb
{
    /// <summary>The refusal of act where no Daoris answers, so no card can be shown.</summary>
    public const string AloneAct =
        "a terminal's presses run at once only once you confirm it in Daoris's window, and Daoris is not running: start it and run this again; nothing was done.";

    public override string Word => TerminalTiers.File;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var tier = TerminalTiers.Named(args.Skip(1).FirstOrDefault())
            ?? throw new ChoiceWords("`settings terminal` needs ask or act — e.g. `daoris settings terminal act`.");
        return new(Word, ChoiceRoutes.Terminal, new JsonObject { ["tier"] = tier }, _ =>
        {
            // With no Daoris running there is no card, and act is granted only on one: alone, only ask is written.
            if (tier == TerminalTiers.Act) throw new ChoiceWords(AloneAct, exit: 1);
            StandingChoices.Write(path, config => config.WithTerminal(tier));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            if (tier == TerminalTiers.Act)
            {
                Say("daoris: a press from a terminal on this machine that no Daoris session owns runs at once as yours, with no card;");
                Say("  each is listed in Notifications. A session's own terminal keeps its tier, and a process inside an agent Daoris runs still asks.");
            }
            else
            {
                Say("daoris: a press from a terminal on this machine waits for your Confirm in Daoris's window.");
            }

            Say($"  Written to {path} — read at each press, so nothing restarts.");
            return 0;
        });
    }

    public override void Listed(DriverConfig choices, DriverChoicesWorld world) => world.Instance.Out.WriteLine(choices.Terminal == TerminalTiers.Act
        ? "  terminal   act  (a terminal's press runs at once as yours, listed in Notifications — `daoris settings terminal ask` asks again)"
        : "  terminal   ask  (a terminal's press waits for your Confirm in the window, the default — `daoris settings terminal act` runs it at once)");
}
