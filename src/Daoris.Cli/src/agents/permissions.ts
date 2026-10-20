// `daoris agent rules` — what an agent Daoris starts may do (PERM1, D72), from a terminal (D50).
//
// A rule is the harness's OWN: a Claude Code permission rule (`Bash(npm run test:*)`,
// `mcp__<server>__<tool>`) in an `allow`, `ask` or `deny` list. Daoris holds rules in three scopes of
// its own — the machine, a circle, a repository — in one file under the home, and the driver hands the
// union to the harness at spawn as its command-line tier. Precedence is the harness's, never a second
// one here: `deny` beats `ask` beats `allow` across every scope the harness merges.
//
// It is the CLI's twin of the driver's `PermissionRules` (`Permissions.cs`). The two artefacts share no code, so THE
// FILE is the contract, and these rules hold in both, each with a test saying so:
//
//   1. Absent is empty; unreadable is empty and says why — and the defaults still hold.
//   2. A rule has one place per scope: added to one list, it leaves the scope's others.
//   3. The union is the defaults still on, the machine's, the session's circle's and its repository's,
//      each rule once in the order first met.
//   4. A key a newer build wrote is kept on every write.
//
// It is a MANAGEMENT verb and it opens no socket and spawns nothing: it reads and writes one file.

import { flagValue, operands as positionals } from '../platform/args.ts';
import { findName, sameName } from '../platform/casefold.ts';
import { DaorisError } from '../platform/errors.ts';
import type { ExitCode } from '../platform/errors.ts';
import { readJsonObject, writeJsonAtomic } from '../platform/fsx.ts';
import { writeKeeping } from '../platform/jsonkeep.ts';
import { requireHomeFile } from '../platform/home.ts';
import { normalizeWorkspace } from '../service/remotemap.ts';
import type { CommandArgs } from '../platform/types.ts';
import { AGENT_ROUTES, ALONE, type VerbDoor } from '../platform/door.ts';

export const PERMISSIONS_FILE = 'permissions.json';

export type RuleList = 'allow' | 'ask' | 'deny';
export type RuleScope = 'machine' | 'workspace' | 'repository';

export interface RuleLists {
  allow: string[];
  ask: string[];
  deny: string[];
}

export interface PermissionDefault {
  id: string;
  list: RuleList;
  rules: string[];
  why: string;
  /** The tools a hook default judges (PERM3). A hook adds no rule, so its `rules` are empty. */
  hook?: string;
}

export interface PermissionFile {
  machine: RuleLists;
  workspaces: Record<string, RuleLists>;
  repositories: Record<string, RuleLists>;
  defaultsOff: string[];
  /** Whatever a newer build wrote here, kept on every write. */
  rest: Record<string, unknown>;
  /** Why the file was read as empty, when it could not be read. */
  problem?: string;
  /**
   * The file as `readPermissions` understood it, written, or absent for rules built here (KEEPALL1): what an edit is measured from, so
   * a write makes only the edit onto the file as it stands.
   */
  understood?: string;
  /** Whether a file was there when it was read (KEEPALL1): with none then and none now, the write writes the rules whole. */
  found?: boolean;
}

/**
 * The connector's own tools. Not `knowledge_refresh`: rebuilding the index is the machine's job. And
 * `permission_propose` (PERM2, D74) only PROPOSES — a widening still waits for the person. `go_ahead_ask`
 * (KNOWUSE1a, D135 §2) only ASKS — the act it names still waits for the person's yes. `review_serve` and `review_ready`
 * (REVIEWENV1b2, D154 points 5 and 8) are a set-up step's, answered for its own session only: serving shows its build to its
 * own tab, and saying what it showed gives no verdict, which is the person's. `opinion_give` and `opinion_answer` (XAGENT1c2,
 * D155; the second agent design §5.4, §6.1, §6.4) are answered for an opinion's own reviewer and for the working session
 * handed it only: each says a claim, and neither asks for an opinion, lands the work or answers a dispute, which is the
 * person's. `helper_ask` and `helper_answer` (MIXAGENT2a-i, D163 point 2) write a file each under the home and nothing else: an ask
 * the driver takes under the person's `helpers` rule, and a helper's answer, which reaches the asker as another agent's claim,
 * never the person's. `channel_post` and `channel_read` (SESSONE2a/2b, D167 point 7) speak and read for the session's own ask
 * only, a helper and a reviewer refused: a message is that agent's claim and asks nothing of anyone, so work stays a quest.
 * `handover_propose` (MIXAGENT1c, D163 point 3) writes one file under the home and nothing else: an ask to go on with another
 * agent, its note the agent's claim, which the person's `handOver` rule settles; a helper and a reviewer are denied it.
 * `line_read` (SESSCTX1f, D189's note) changes nothing: it reads the event files of the session's own line on this machine, what
 * its brief's bound left out, and the service refuses every other record; a helper and a reviewer are denied it.
 */
const CONNECTOR_TOOLS = [
  'registry', 'knowledge_search', 'knowledge_get', 'knowledge_repositories',
  'knowledge_convergence', 'quest_list', 'quest_respond', 'quest_publish', 'permission_propose', 'go_ahead_ask',
  'review_serve', 'review_ready', 'opinion_give', 'opinion_answer', 'helper_ask', 'helper_answer', 'channel_post', 'channel_read',
  'handover_propose', 'line_read',
];

/** What Daoris ships. 🔴 The driver's `PermissionRules` (`Permissions.cs`) holds the same table, and a test reads this one. */
export const DEFAULTS: readonly PermissionDefault[] = [
  {
    id: 'connector',
    list: 'allow',
    rules: CONNECTOR_TOOLS.map((tool) => `mcp__daoris-knowledge__${tool}`),
    why: 'A session takes and closes its own quest, publishes what it finds for others and proposes a '
      + "change to these rules, through Daoris's connector — and anything it would have to ask for is "
      + 'refused.',
  },
  // 🔴 The owner's answer to PERM4 (2026-09-24): in a folder the agent never trusted, a real driven
  // session made its edit and was refused the commit, because the repository's own allow-list does not
  // apply there. `cd` because the agent prefixes its commit with one, and every part must be allowed.
  // `git mv` (UNBLOCK4, D122 §3.6): a rename stays in the tree and git refuses a path outside it.
  {
    id: 'commit',
    list: 'allow',
    rules: ['Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)', 'Bash(git mv:*)'],
    why: 'A session commits its own work in its own tree, because committing is part of finishing a task '
      + '— the push is still refused.',
  },
  // 🔴 UNBLOCK4 (D122 §3.7): a Bash rule matches the command as written, so `git push …` alone let
  // `git -C . push` and `git -c <key>=<value> push` through. The option-first rules take every push with
  // options before its subcommand. The driver also tells auto mode's classifier, in the spawn file only
  // it writes, that a push in any form, a publish and a release are the person's; that sentence has no
  // twin here.
  {
    id: 'no-push',
    list: 'deny',
    rules: ['Bash(git push)', 'Bash(git push:*)', 'Bash(git -* push)', 'Bash(git -* push *)'],
    why: 'A push leaves this machine, and that stays the person\'s call.',
  },
  // PERM3: a hook the driver hands at spawn, not a rule — a rule cannot say "outside the tree".
  {
    id: 'tree-guard',
    list: 'deny',
    rules: [],
    why: 'A session writes files only inside its own tree: an edit or a write anywhere else is refused, '
      + 'through links as well. A change needed elsewhere is a quest.',
    hook: 'Edit|Write|MultiEdit|NotebookEdit',
  },
];

const LISTS: readonly RuleList[] = ['allow', 'ask', 'deny'];
const SHAPE = /^[A-Za-z][A-Za-z0-9_-]*(\([^\r\n]+\))?$/;
const empty = (): RuleLists => ({ allow: [], ask: [], deny: [] });

/** Where the rules live: the file under the Daoris home (D63), or a refusal naming what to set. */
export function permissionsPath(env: Record<string, string | undefined> = process.env): string {
  return requireHomeFile(env, 'permissions');
}

/** Why a rule is refused, or null when it is the harness's shape. */
export function ruleRefusal(rule: string): string | null {
  return SHAPE.test(rule ?? '')
    ? null
    : `\`${rule}\` is not a permission rule — one is a tool name with an optional specifier in `
      + 'parentheses, like `Bash(npm run test:*)`, `Edit(/src/**)` or `mcp__daoris-knowledge__quest_list`.';
}

/** The file as it stands: absent is empty, and unreadable is empty and says why. */
export function readPermissions(path: string): PermissionFile {
  const nothing: PermissionFile = { machine: empty(), workspaces: {}, repositories: {}, defaultsOff: [], rest: {} };
  const { value, problem } = readJsonObject(path);
  if (problem !== null) {
    return { ...nothing, problem: `${problem}, so no rule of it applies. The defaults still do.` };
  }
  if (value === null) return { ...nothing, understood: JSON.stringify(permissionsWritten(nothing)), found: false };

  const { machine, workspaces, repositories, defaultsOff, ...rest } = value;
  const file: PermissionFile = {
    machine: lists(machine),
    workspaces: scopes(workspaces),
    repositories: scopes(repositories),
    defaultsOff: strings(defaultsOff),
    rest,
  };
  return { ...file, understood: JSON.stringify(permissionsWritten(file)), found: true };
}

/**
 * Written beside and renamed over, BOM-less and LF; an empty scope is left out — and never over a file it could not read. The edit from
 * what was read is made onto the file as it stands (KEEPALL1, `jsonkeep.ts`' `writeKeeping`), so a key this build has no field for
 * stays where it is at every level, every number it did not change by its own text, a change another writer made since the read stays,
 * and no edit writes nothing: rebuilt from the fields it reads, it moved what it did not know to the front and deleted a scope's own
 * unknown keys. Rules built here are written whole. The driver's `PermissionRules.Save` does the same.
 */
export function writePermissions(path: string, file: PermissionFile): void {
  if (file.understood === undefined) writeJsonAtomic(path, permissionsWritten(file));
  else writeKeeping(path, file.found === true, JSON.parse(file.understood) as Record<string, unknown>, permissionsWritten(file));
}

/** The rules' own shape: what neither knows first, then each scope only where it holds a rule, as the driver's `PermissionRules.Root`. */
function permissionsWritten(file: PermissionFile): Record<string, unknown> {
  const node = (held: RuleLists) => Object.fromEntries(LISTS.filter((list) => held[list].length > 0).map((list) => [list, held[list]]));
  const named = (map: Record<string, RuleLists>) => Object.fromEntries(
    Object.entries(map).filter(([, held]) => !isEmpty(held)).sort(([a], [b]) => (a < b ? -1 : 1)).map(([key, held]) => [key, node(held)]));

  const out: Record<string, unknown> = { ...file.rest };
  if (!isEmpty(file.machine)) out.machine = node(file.machine);
  if (Object.keys(named(file.workspaces)).length > 0) out.workspaces = named(file.workspaces);
  if (Object.keys(named(file.repositories)).length > 0) out.repositories = named(file.repositories);
  if (file.defaultsOff.length > 0) out.defaultsOff = [...new Set(file.defaultsOff)].sort();
  return out;
}

/**
 * The union one session is handed: the defaults still on, the machine's rules, its circle's and its
 * repository's — each rule once, in the order first met.
 */
export function composeRules(file: PermissionFile, workspace: string | null, repository: string | null): RuleLists {
  const layers: RuleLists[] = DEFAULTS
    .filter((shipped) => !file.defaultsOff.includes(shipped.id))
    .map((shipped) => ({ ...empty(), [shipped.list]: shipped.rules }));
  layers.push(file.machine);
  // A scope is a person's name, matched in any case — every matching one, as the driver's `Compose` does, by its
  // `OrdinalIgnoreCase` (`casefold.ts`, CASEFOLD1).
  const named = (scopes: Record<string, RuleLists>, name: string) =>
    Object.entries(scopes).filter(([held]) => sameName(held, name)).map(([, lists]) => lists);
  layers.push(...named(file.workspaces, normalizeWorkspace(workspace)));
  if (repository?.trim()) layers.push(...named(file.repositories, repository.trim()));

  const union = (list: RuleList) => [...new Set(layers.flatMap((layer) => layer[list]))];
  return { allow: union('allow'), ask: union('ask'), deny: union('deny') };
}

/** A rule into one list of one scope — and out of that scope's other lists. */
export function addRule(
  file: PermissionFile, scope: RuleScope, name: string | null, list: RuleList, rule: string,
): PermissionFile {
  const refused = ruleRefusal(rule);
  if (refused) throw new DaorisError(refused);
  return edit(file, scope, name, (held) => {
    const without = drop(held, rule);
    return { ...without, [list]: [...without[list], rule] };
  });
}

/** A rule out of every list of one scope. */
export function removeRule(file: PermissionFile, scope: RuleScope, name: string | null, rule: string): PermissionFile {
  return edit(file, scope, name, (held) => drop(held, rule));
}

/** A default switched on or off for this machine, by the id it ships under. */
export function switchDefault(file: PermissionFile, id: string, on: boolean): PermissionFile {
  if (!DEFAULTS.some((shipped) => shipped.id === id)) {
    throw new DaorisError(`no default \`${id}\` — Daoris ships ${DEFAULTS.map((d) => `\`${d.id}\``).join(', ')}.`);
  }
  const off = file.defaultsOff.filter((existing) => existing !== id);
  return { ...file, defaultsOff: on ? off : [...off, id] };
}

/**
 * `daoris agent rules …` — list, add to a list, remove, or switch a default. An edit is judged here, then goes through `door`
 * (RPC1f): the rules screen's `RULE_ACTION` while Daoris runs on this home, written here under the home's lease where none does.
 */
export function commandRules({ argv, write }: CommandArgs, door: VerbDoor = ALONE): ExitCode | Promise<ExitCode> {
  const verb = argv[0] ?? 'list';
  const path = permissionsPath();
  const file = readPermissions(path);
  /** One edit, judged and made here as `next`, or sent as the screen sends it, said the same either way. */
  const press = (payload: Record<string, unknown>, next: PermissionFile, said: () => ExitCode) => door({
    what: `agent rules ${verb}`, route: AGENT_ROUTES.rule, payload,
    alone: () => {
      writePermissions(path, next);
      return said();
    },
    words: said,
  });

  const workspace = flagValue(argv, '--workspace');
  const repository = flagValue(argv, '--repository');
  if (workspace !== undefined && repository !== undefined) {
    throw new DaorisError('a rule reaches one scope — name a workspace with `--workspace` or a repository with `--repository`, not both.');
  }
  const scope: RuleScope = workspace !== undefined ? 'workspace' : repository !== undefined ? 'repository' : 'machine';
  const name = workspace ?? repository ?? null;
  const where = scope === 'machine' ? 'every session on this machine' : `${scope === 'workspace' ? 'workspace' : 'repository'} \`${name}\``;

  switch (verb) {
    case 'list':
      return list();

    case 'allow':
    case 'ask':
    case 'deny': {
      const [rule] = operands('<rule>');
      return press({ action: 'add', scope, ...(name !== null ? { name } : {}), list: verb, rule }, addRule(file, scope, name, verb, rule!), () => {
        write(`daoris: \`${rule}\` is now ${verb === 'allow' ? 'allowed' : verb === 'ask' ? 'asked for' : 'denied'} for ${where}.`);
        write('  Sessions started from now on are handed it; one already running keeps what it began with.');
        if (verb === 'ask') {
          write('  An ask is a refusal for anything Daoris starts — nobody is there to answer it.');
        }
        return 0;
      });
    }

    case 'remove': {
      const [rule] = operands('<rule>');
      return press({ action: 'remove', scope, ...(name !== null ? { name } : {}), rule }, removeRule(file, scope, name, rule!), () => {
        write(`daoris: \`${rule}\` is no longer one of the rules for ${where}.`);
        return 0;
      });
    }

    case 'default': {
      const [id, state] = operands('<id>');
      if (state !== 'on' && state !== 'off') {
        throw new DaorisError(`say whether the default \`${id}\` is on or off: \`daoris agent rules default ${id} on|off\`.`);
      }
      return press({ action: 'default', id, on: state === 'on' }, switchDefault(file, id!, state === 'on'), () => {
        write(`daoris: the default \`${id}\` is ${state} on this machine.`);
        return 0;
      });
    }

    default:
      throw new DaorisError(
        `unknown rules verb '${verb}' — one of: list, allow, ask, deny, remove, default, proposals, accept, decline`);
  }

  /** The bare tokens after the verb — a flag and its value are not operands. At least one is needed. */
  function operands(what: string): string[] {
    const bare = positionals(argv, new Set(['--workspace', '--repository'])).slice(1);
    if (bare.length === 0) throw new DaorisError(`\`daoris agent rules ${verb}\` needs a ${what}.`);
    return bare;
  }

  function list(): ExitCode {
    write(`What an agent Daoris starts may do on this machine — ${path}`);
    write('  Claude Code\'s own permission rules. Sessions on other agents are handed none of these.');
    if (file.problem) write(`  ⚠ ${file.problem}`);
    write('');
    write('Defaults');
    for (const shipped of DEFAULTS) {
      const on = !file.defaultsOff.includes(shipped.id);
      const what = shipped.hook ? `a hook on ${shipped.hook}` : shipped.rules.join(', ');
      write(`  ${shipped.id.padEnd(10)} ${on ? 'on ' : 'off'}  ${shipped.list}  ${what}`);
    }
    show('This machine', file.machine);
    for (const [circle, held] of Object.entries(file.workspaces)) show(`workspace \`${circle}\``, held);
    for (const [repo, held] of Object.entries(file.repositories)) show(`repository \`${repo}\``, held);
    return 0;
  }

  function show(title: string, held: RuleLists): void {
    if (isEmpty(held)) return;
    write('');
    write(title);
    for (const list of LISTS) for (const rule of held[list]) write(`  ${list.padEnd(5)}  ${rule}`);
  }
}

function edit(file: PermissionFile, scope: RuleScope, name: string | null, change: (held: RuleLists) => RuleLists): PermissionFile {
  if (scope === 'machine') return { ...file, machine: change(file.machine) };
  if (!name?.trim()) {
    throw new DaorisError(`a ${scope === 'workspace' ? 'workspace' : 'repository'} scope needs its name.`);
  }

  const field = scope === 'workspace' ? 'workspaces' : 'repositories';
  // The scope's existing spelling, in any case: one scope, never two — the driver's `Edit` twin (REV3).
  const key = findName(Object.keys(file[field]), name.trim()) ?? name.trim();
  const next = { ...file[field], [key]: change(file[field][key] ?? empty()) };
  if (isEmpty(next[key]!)) delete next[key];
  return { ...file, [field]: next };
}

function drop(held: RuleLists, rule: string): RuleLists {
  return {
    allow: held.allow.filter((r) => r !== rule),
    ask: held.ask.filter((r) => r !== rule),
    deny: held.deny.filter((r) => r !== rule),
  };
}

function isEmpty(held: RuleLists): boolean {
  return held.allow.length === 0 && held.ask.length === 0 && held.deny.length === 0;
}

function strings(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0).map((item) => item.trim())
    : [];
}

/** A hand edit's rule that is not the shape is left out rather than handed to the harness. */
function lists(value: unknown): RuleLists {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return empty();
  const held = value as Record<string, unknown>;
  const rules = (list: RuleList) => [...new Set(strings(held[list]).filter((rule) => ruleRefusal(rule) === null))];
  return { allow: rules('allow'), ask: rules('ask'), deny: rules('deny') };
}

function scopes(value: unknown): Record<string, RuleLists> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const out: Record<string, RuleLists> = {};
  for (const [key, held] of Object.entries(value as Record<string, unknown>)) {
    const parsed = lists(held);
    if (key.trim() && !isEmpty(parsed)) out[key.trim()] = parsed;
  }
  return out;
}
