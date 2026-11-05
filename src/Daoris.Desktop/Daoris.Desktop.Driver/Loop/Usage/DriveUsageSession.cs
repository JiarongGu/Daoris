namespace Daoris.Driver;

/// <summary>The usage's <c>session</c> and <c>settings</c> verbs.</summary>
internal static class DriveUsageSession
{
    public const string Text =
        """
          session list [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
              this machine's sessions by what they need: waiting on you first, then to review, working,
              resumes later and ended.
          session stop <id> [--turn]  ·  session finish <id> [--note "…"]  ·  session decline <id> --reason "…"
              stop a live session (its quest is then held here until `daoris quest retry`), or finish or
              decline one that waits on you. --turn stops only its turn, keeping the session, so the words
              waiting for its turn's end go next, where its door can.
          session archive <id>… | --ended [--yes]  ·  session unarchive <id>…  ·  session delete <id>
              take ended sessions out of the list, or bring them back; delete a conversation that served no quest.
          session say <id> "…" [--file <path>]… [--level ordinary|urgent|now]
              say something to a session of this machine's: read at its next step or when its turn ends while it
              works, or, parked or ended, the same session goes on with it: a park's answer is said here. Prints
              where the words stand. --level is when it reads them while it works: ordinary at its turn's end,
              urgent (the default) at its next step, now cutting its step where its agent steers; no level stops
              its turn. It says what the door did, and why where it could not.
          session take-back <id> [<word>]  ·  session reword <id> <word> "…"
              take back one of your words waiting on a parked or ended session before it goes on with them, or put
              new words in its place; with no word, list those waiting with their ids.
          session hand-over <id> [--agent <adapter> [--account <name>]]
              hand a session over to the agent named, or, naming none, let words that wait for the account it ran
              on to cool go on now in a new session at the driver's next look, handed them, without its conversation.
          session at-work <session> [--json]
              who is at work on a session: its line's parts, what it waits on of others and its co-workers, as the
              window's *At work* draws them (COWORKVIEW1a, COMMS1i).
          session start-from <id>
              words a session cannot go on with: start a conversation here with them, as `chat` does, without that
              session's context; they leave that session, naming the conversation.
          session hand-over <id> --agent <adapter> [--account <name>]
              a conversation goes on with another agent (D163): its own ends, and a new conversation in its tree
              starts, told what Daoris kept of it. Conversations only; a quest's session comes later.
          session hand-over <id> --approve|--decline
              your yes or no to a session's agent's own ask to hand it over that waits on you (the `handOver` rule's
              `ask`), as its page takes it: a yes hands it to the agent named at the driver's next look; a no says so.
          session helper <id> "<question>" [--agent <name>]
              ask another agent, read-only, to answer one question about a session's work (D163): the driver takes
              it at its next look, and the answer comes back to that session as its next turn, marked as the helper's.
          session helper --approve|--decline <ask>
              your yes or no to an agent's ask for a helper that waits on you (the `helpers` rule's `ask`), as its
              session's page takes it: a yes starts it at the driver's next look; a no refuses it and says so.
          session partner <id> "<piece>" --agent <name> [--paths <path>…]
              ask another agent to write one piece for a session in a clone of its own (D199): the driver judges it
              at its next look and starts it only where that agent's confinement was measured; its answer comes back
              to that session naming the commit to merge.
          session partner <id> --list  ·  session partner <piece> --stop
              a session's pieces and where each stands; a working piece's stop, which keeps its work.
          session handback <session|quest> [--json]
              one quest's handback whole (D219), by the quest's id or the id of the session that served it, open or
              closed: the note its session closed it with, how it answered each requirement, when and by which
              session. --json prints the service's answer as it gave it.
          session opinion ask <session> [--agent <adapter>] [--same-agent] ["…"]  ·  session opinion show <session|opinion>
          session opinion stop <opinion>  ·  session opinion anyway <session> ["…"]  ·  session opinion myself <session> ["…"]
              a session's second opinion before its work lands: ask another agent to read it (one of its rule's reviewers by
              name, or the working agent fresh), say where it stands with its findings and their answers, stop a reviewer
              reading, go on without a settled one, or record your own reading in its place. `session say` sends it back.
          session supervise [<id>]  ·  session supervise <id> --question <qid> --generation <g> --choice <label>|--leave <bound> (--reason "…" --source <where>)…
              the supervisor's holder's verb (D208), only in the terminal Daoris started for it: what waits, one
              question's fenced print, and the answer or the question left to the owner. No alone path.
          settings notify on|off  ·  settings cooloff <minutes>  ·  settings cap <n>
          settings keep [<kind> <rule>]  ·  settings handover …  ·  settings brainstorms …
          settings actor found [take|dismiss <n>]
              this machine's standing choices: whether a park or an end you did not ask for is said, how long an
              account cools, the cap, how long each kind is kept, a hand-over's and a brainstorm's rules, and the
              staffing copy's found changes; the agents each kind of work runs on are `role set`'s.
        """;
}
