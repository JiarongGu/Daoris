using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The machine the room's section tests render (MOD6): two workspaces, an agent signed in and one not installed.</summary>
internal static class HelpRoomFixture
{
    public static readonly HelpMachine Machine = new()
    {
        Adapter = "claude-code",
        Intake = "claude-code-acp",
        Helper = "claude-code-acp",
        Cap = 4,
        // ENTRY1f2: by id and where each runs, an intake by its ask.
        Waiting = [new("s1a2b3c4", "console-ui"), new("s7a8b9c0", "ask #a1") { Ask = "a1" }],
        // SESSUX1h3: the sessions by the list's groups, and the ended ones by id.
        Sessions = new(2, 1, 3, 0, 2, [new("s2b3c4d5", "engine"), new("s3c4d5e6", "console-ui")]),
        Asks = 1,
        Repositories =
        [
            new("console-ui", "work")
            {
                Checkout = true, Drivable = true, OwnTree = true,
                Line = new Line("feature/app", LineSource.Repository),
                Landing = new Landing(new LandingRule("branch", "feature/{slug}-{quest}", Tidy: true), LandingSource.Workspace),
            },
            new("reports-db", "work")
            {
                Checkout = true, Drivable = true, Held = true, Line = new Line("main", LineSource.Checkout),
            },
            new("engine", "default") { Checkout = false },
        ],
        // Reading across on, the default (D107): every checkout here, one of them with a space in its path.
        Reads = [new("console-ui", "work", "/work/console-ui"), new("reports-db", "work", "/work/reports db")],
        Agents =
        [
            new("claude-code")
            {
                Product = "Claude Code", Present = true, Version = "2.1.0",
                Accounts = [new("work", "out")],
            },
            new("codex") { Present = false },
        ],
    };
}
