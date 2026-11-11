namespace Daoris.Driver;

/// <summary>The usage's <c>ask</c> and <c>ask-daoris</c> groups.</summary>
internal static class DriveUsageAsk
{
    public const string Text =
        """
          ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… "…"
          ask publish <id> --to <repo>  ·  ask close <id> --reason "…"  ·  ask delete <id>
          ask go-ahead <id> <n> approve|refuse ["…"]
              ask at a workspace, or turn an ask into a quest, close it, or delete one made by mistake; answer a
              go-ahead a session asked on it, which every session on the ask is handed from then on, and which
              sends a parked session on once none of its go-aheads waits.
          ask channel <id> [--to <repository>] [--re <message>] [--ordinary] ["…"]
              read an ask's channel, where its sessions and you talk, with each message's level where it is urgent
              and what it reached, and mark it read; or with words post them as yours, to everyone on the ask or
              one of its repositories, which each session reads at its next step, or with --ordinary when its turn ends.
          ask at-work <id> [--json]
              who is at work on an ask: each line of work counted by its parts and subagents, with the session
              that opens it, and the quests the ask asked with who took each; --json prints the answer whole. The
              channel and why a quest is not taken yet are not in it.
          ask pause <id>  ·  ask resume <id>
              stop what of an ask's work runs on this machine and start nothing of it until you resume it,
              which carries it on where it stood.
          ask abandon <id> [--reason "…" --yes]
              list what abandoning it would decline, discard and archive, and what it keeps and why; with
              --reason and --yes, give up what the list holds, each quest declined with your reason.
          ask … --short "…"
              name a new ask in a list by a short title, at most 40 characters on one line: the service judges it, as the
              composer's field is judged.
          ask … --review rule|on|<environment>|off [--review-words "…"]
          ask review <id> on|<environment>|off ["…"]
              choose whether work is reviewed before it lands, and where: a done quest's at its gate, where off is the
              skip and on (the repository's default) or an environment it declares publishes a set-up step to show it
              there; a new ask's, where rule (the default) leaves it to each repository's rule; or an ask's, the latest
              standing, which applies its intake's proposal too.
          ask … --kind <kind> [--workflow <id>|current] [--workflow-words "…"]
          ask workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear ["…"]
              say what kind of task an ask is and which workflow its work follows: a kind its workspace declares, a
              workflow saved here or current; the latest stands, and each run binds what is chosen at its first start.
              Yours alone: an agent proposes a kind, never sets one.
          ask clear <id> [--yes]
              list what clearing one ask's work from this machine would take and keep; with --yes, clear it.
          ask-daoris list [--search "…"] [--json] [--offset <n>] [--limit <n>] [--workspace <name> | --all | --about <place>]
          ask-daoris rename <id> "…" | --clear  ·  ask-daoris pin <id>  ·  ask-daoris unpin <id>
          ask-daoris focus <id> machine | <workspace> | <workspace>/<repository>
          ask-daoris delete <id> [--yes]
              Ask Daoris's conversations, kept on this machine only: list or search them a page at a time, one
              workspace's, every one's, or those about a place (home or any focus held, or inside it), each with its
              own workspace and focus; name, pin or unpin one, set what the next words are about, or delete one,
              listed first and deleted with --yes. Words to one are `session say <id>`'s.
          ask-daoris mode [restricted|full]
              Ask Daoris's access mode (D183), from its next start: restricted, the default, reads and proposes and
              you apply; full acts on this machine itself, in its agent's mode that asks nothing. Alone, says which.
        """;
}
