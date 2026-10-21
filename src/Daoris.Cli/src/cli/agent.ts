import type { CliCommand } from '../platform/types.ts';
import { commandHarness, runDriverProgram } from '../agents/toolchain.ts';
import { releaseFetcher } from '../service/service.ts';

export const command: CliCommand = {
  name: 'agent',
  kind: 'management',
  // The tools a session runs are agents to a person; `harness` stays the code's word (AGT1).
  formerly: ['harness'],
  usage: [
    "  agent [verb]         this machine's agents — Claude Code, Codex, dsh — and the",
    "                       accounts they run as. A sign-in stays the tool's:",
    '                         list                      installed? version? accounts?',
    '                         install <agent>           its OWN installer, never auto',
    '                         update <agent> [--workspace W]',
    '                                                   pinned: the pin moves to the',
    '                                                   newest release, resolved to',
    '                                                   one exact version; unpinned:',
    "                                                   the agent's own updater",
    '                         login <agent> [--profile P] [--join W] [--join-machine]',
    '                                                   its own login flow, run INTO',
    "                                                   an account that is here, by",
    '                                                   its id or name, else the',
    '                                                   default; never a new folder',
    '                         login <agent> --new [--name N] [--join W] [--join-machine]',
    '                                                   sign in to a new account:',
    '                                                   kept only if it finished;',
    '                                                   offers who signed in as its',
    '                                                   name; --join puts it in a',
    "                                                   workspace's list, and the end",
    '                                                   says which lists hold it',
    '                         key <agent>               an account that is an API key,',
    '                                                   read from stdin; Daoris keeps it',
    '                         key <agent> --account <id|name>',
    '                                                   a new key for that key account,',
    '                                                   from stdin: its name, lists and',
    '                                                   defaults stay, a refusal clears',
    '                         profile list|add|remove <agent> <profile>',
    '                                                   remove deletes the account,',
    '                                                   sign-in and all',
    '                         profile default <agent> <profile>|--clear [--workspace W]',
    "                                                   --clear names none again: the",
    "                                                   agent's own home, or for a",
    "                                                   workspace the machine's default",
    '                         profile order <agent> <profile>…|--clear [--workspace W]',
    '                                                   the accounts rotation may use,',
    '                                                   in order; one not listed never',
    '                                                   rotates, into or away',
    '                         profile use <agent> [goal|order] [--keep <account>|--no-keep]',
    '                               [--switch smart|curve|off] [--curve <window>=<e>:<u>,…]…',
    '                               [--no-curve] [--workspace W]',
    '                                                   how a list is used: goal, make',
    '                                                   the most of its accounts, or',
    '                                                   order, one by one in order;',
    '                                                   one kept for conversations;',
    '                                                   when to switch: smart, your',
    '                                                   own curve per window (session,',
    '                                                   weekly), or off. --early and',
    '                                                   --near are the old names. No',
    '                                                   flag prints them; --clear',
    "                                                   returns to today's defaults",
    '                         profile ready <agent> <profile>|--own',
    "                                                   end an account's cool-off now:",
    "                                                   --own is the tool's own sign-in",
    '                         profile rename <agent> <account> <name>',
    "                                                   an account's name, yours: its",
    '                                                   id stays, so every list,',
    '                                                   default and record keeps it;',
    '                                                   its own id gives it none',
    '                         profile join <agent> <account> [<workspace>…] [--machine]',
    "                                                   put an account in a workspace's",
    "                                                   list, or this machine's",
    '                         pin <agent> <version> [--workspace W]',
    '                                                   install that version somewhere',
    '                                                   Daoris owns, and run it. Claude',
    '                                                   Code and Codex come from their',
    "                                                   makers' own channels, verified",
    '                         unpin <agent> [--workspace W]',
    '                                                   back to whatever is on PATH',
    '                         which <agent> [--workspace W] [--json]',
    '                                                   the program a session would run',
    '                                                   it as: its command, its pin, or',
    "                                                   PATH's",
    '                         trust <agent> <folder> [--profile P] --yes',
    '                                                   what the agent asks the first',
    '                                                   time it runs in a folder, granted',
    '                                                   in its own file; without --yes,',
    '                                                   the question and nothing written',
    '                         settings <agent> [--account A] [model M] [effort E [--for M]]',
    "                                                   an account's own model and",
    "                                                   effort, in the tool's own",
    '                                                   settings file; unset clears,',
    '                                                   and given neither, it prints them',
    '                         rules [allow|ask|deny|remove <rule>]',
    '                               [--workspace W | --repository R]',
    '                                                   what a session Daoris starts may',
    '                                                   do; rules default <id> on|off',
    '                         rules proposals|accept|decline <id>',
    '                                                   what agents asked to change',
  ],
  options: [
    '  --profile <name>     which account to act on (agent login) — a named,',
    '                       isolated configuration home for that agent; any',
    '                       verb takes an account by its id or its name',
    '  --name <name>        a new account\'s name, as it is made (agent login --new)',
    '  --join <workspace>   a list a sign-in puts its account in, at its end;',
    '                       repeated or comma-separated. --join-machine is this',
    "                       machine's list (agent login)",
    "  --device-code        sign in by the agent's device code, not its browser,",
    '                       where it has one (agent login codex)',
  ],
  // PUSH1f2d (D194 point 1): `rules`, `trust` and `settings` are Daoris's driver program's, `daoris-driver agent` with the same words;
  // this writes nothing before it starts; so are `which`, `unpin` and every `profile` verb (PUSH1f2e1, PUSH1f2e2, PUSH1f2e3), so no
  // verb left here takes a route, and `list` with a bare `daoris agent` (PUSH1f2e4), and `install` and `key` (PUSH1f2e5), whose key
  // the program reads from the terminal's own standard input, and `login` (PUSH1f2e6), whose prompts the agent asks at this terminal.
  // The rest stays here until the rest of PUSH1f2e: `pin` and `update`, which a pin's move makes one with `pin`. `pin` and a pinned `update` fetch a maker's release
  // (AGT2b, USE1a) through the one module that may reach a network, handed in here so the toolchain never holds a socket of its own.
  run: (args) => handed(args.argv)
    ? (args.driverProgram ?? ((family, words) => runDriverProgram(family, words)))('agent', args.argv)
    : commandHarness(args, releaseFetcher(), ['npm']),
};

/**
 * The verbs `daoris agent` hands Daoris's driver program (PUSH1f2d, the alone-path design §5's allow-list): this machine's agents
 * (PUSH1f2e4), the rules, a trust, an account's settings and what an agent runs as (PUSH1f2e1), an unpin and every account verb
 * (PUSH1f2e2, PUSH1f2e3), an agent's install and an API key's account (PUSH1f2e5), an agent's sign-in (PUSH1f2e6), each one `daoris-driver agent` has
 * (`AgentsCommand.Verbs`, which a test reads beside this).
 */
export const HANDED_VERBS: readonly string[] = Object.freeze([
  'list', 'rules', 'trust', 'settings', 'which', 'unpin', 'profile', 'install', 'key', 'login',
]);

/** Whether these words are the driver program's: a handed verb. None named is `list`, handed too since PUSH1f2e4. */
export function handed(argv: readonly string[]): boolean {
  return HANDED_VERBS.includes(argv[0] ?? 'list');
}
