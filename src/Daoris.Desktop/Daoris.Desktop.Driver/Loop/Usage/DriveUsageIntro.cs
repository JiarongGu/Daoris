namespace Daoris.Driver;

/// <summary>The usage's opening, <c>help</c> and the <c>agent</c> group.</summary>
internal static class DriveUsageIntro
{
    public const string Text =
        """
        usage: daoris <command> …

          <command> …  ·  --version
              a word no group below owns is the CLI's, a doctrine command, offline: run by the CLI this install
              carries, on the node PATH finds (Node.js 22 or later); `daoris help` ends with the CLI's usage.
          <plugin> <command> … [--stdin]
              a command an installed plugin declares, the plugin named by its id, or by its id without its family where no
              other installed plugin's id ends the same (`daoris azure-devops login` for `daoris-azure-devops`).
          help [<group>]
              this usage, or one group's.
          agent list  ·  agent install|update|pin|unpin|which <agent> …  ·  agent login|key <agent> …
          agent rules [list | allow|ask|deny|remove <rule> | default <id> on|off | proposals | accept|decline <id>]
          agent trust <agent> <folder> [--account <name>] [--yes]  ·  agent settings <agent> [--account <name>] [model|effort …]
          agent account list|add|remove|default|order|use|ready|rename|join <agent> …
              this machine's agents: their installs, pins and sign-ins, their rules, trust and settings, and each
              agent's accounts; a change a route takes goes to the running Daoris's window to confirm where one runs,
              and is written here where none does; `agent --help` says each verb's words.
        """;
}
