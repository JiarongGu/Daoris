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
        Waiting = 2,
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
        Agents =
        [
            new("claude-code")
            {
                Product = "Claude Code", Present = true, Version = "2.1.0", Login = "in",
                Accounts = [new("work", "out")],
            },
            new("codex") { Present = false, Login = "unknown" },
        ],
    };
}
