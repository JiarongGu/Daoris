// `daoris agent rules` — what an agent Daoris starts may do (PERM1, D72), from a terminal (D50).
//
// A rule is the harness's OWN: a Claude Code permission rule (`Bash(npm run test:*)`,
// `mcp__<server>__<tool>`) in an `allow`, `ask` or `deny` list. Daoris holds rules in three scopes of
// its own — the machine, a circle, a repository — in one file under the home, and the driver hands the
// union to the harness at spawn as its command-line tier. Precedence is the harness's, never a second
// one here: `deny` beats `ask` beats `allow` across every scope the harness merges.
//
// It is the CLI's twin of the driver's `PermissionRules.cs`. The two artefacts share no code, so THE
// FILE is the contract, and these rules hold in both, each with a test saying so:
//
//   1. Absent is empty; unreadable is empty and says why — and the defaults still hold.
//   2. A rule has one place per scope: added to one list, it leaves the scope's others.
//   3. The union is the defaults still on, the machine's, the session's circle's and its repository's,
//      each rule once in the order first met.
//   4. A key a newer build wrote is kept on every write.
//
// It is a MANAGEMENT verb and it opens no socket and spawns nothing: it reads and writes one file.

import { existsSync, readFileSync } from 'node:fs';
import { flagValue } from './args.ts';
import { DaorisError } from './errors.ts';
import type { ExitCode } from './errors.ts';
import { writeTextAtomic } from './fsx.ts';
import { requireHomeFile } from './home.ts';
import { normalizeWorkspace } from './remotemap.ts';
import type { CommandArgs } from './types.ts';

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
}

/**
 * The connector's own tools. Not `knowledge_refresh`: rebuilding the index is the machine's job. And
 * `permission_propose` (PERM2, D74) only PROPOSES — a widening still waits for the person.
 */
const CONNECTOR_TOOLS = [
  'registry', 'knowledge_search', 'knowledge_get', 'knowledge_repositories',
  'knowledge_convergence', 'quest_list', 'quest_respond', 'quest_publish', 'permission_propose',
];

/** What Daoris ships. 🔴 The driver's `PermissionRules.cs` holds the same table, and a test reads this one. */
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
  {
    id: 'commit',
    list: 'allow',
    rules: ['Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)'],
    why: 'A session commits its own work in its own tree, which D37 makes automatic — the push is still '
      + 'refused.',
  },
  {
    id: 'no-push',
    list: 'deny',
    rules: ['Bash(git push)', 'Bash(git push:*)'],
    why: 'A push leaves this machine, and that stays the person\'s (D37).',
  },
  // PERM3: a hook the driver hands at spawn, not a rule — a rule cannot say "outside the tree".
  {
    id: 'tree-guard',
    list: 'deny',
    rules: [],
    why: 'A session writes files only inside its own tree: an edit or a write anywhere else is refused, '
      + 'through links as well (D51). A change needed elsewhere is a quest.',
    hook: 'Edit|Write|MultiEdit|NotebookEdit',
  },
];

const LISTS: readonly RuleList[] = ['allow', 'ask', 'deny'];
const SHAPE = /^[A-Za-z][A-Za-z0-9_-]*(\([^\r\n]+\))?$/;
const empty = (): RuleLists => ({ allow: [], ask: [], deny: [] });

/** Where the rules live: the file under the Daoris home (D63), or a refusal naming what to set. */
export function permissionsPath(env: Record<string, string | undefined> = process.env): string {
  return requireHomeFile(env, PERMISSIONS_FILE);
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
  if (!existsSync(path)) return nothing;

  try {
    const parsed = JSON.parse(readFileSync(path, 'utf8')) as unknown;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return { ...nothing, problem: `${PERMISSIONS_FILE} is not a JSON object, so no rule of it applies.` };
    }

    const { machine, workspaces, repositories, defaultsOff, ...rest } = parsed as Record<string, unknown>;
    return {
      machine: lists(machine),
      workspaces: scopes(workspaces),
      repositories: scopes(repositories),
      defaultsOff: strings(defaultsOff),
      rest,
    };
  } catch (error) {
    return {
      ...nothing,
      problem: `${PERMISSIONS_FILE} could not be read (${(error as Error).message}), so no rule of it `
        + 'applies. The defaults still do.',
    };
  }
}

/** Written beside and renamed over, BOM-less and LF; an empty scope is left out. */
export function writePermissions(path: string, file: PermissionFile): void {
  const node = (held: RuleLists) => Object.fromEntries(LISTS.filter((list) => held[list].length > 0).map((list) => [list, held[list]]));
  const named = (map: Record<string, RuleLists>) => Object.fromEntries(
    Object.entries(map).filter(([, held]) => !isEmpty(held)).sort(([a], [b]) => (a < b ? -1 : 1)).map(([key, held]) => [key, node(held)]));

  const out: Record<string, unknown> = { ...file.rest };
  if (!isEmpty(file.machine)) out.machine = node(file.machine);
  if (Object.keys(named(file.workspaces)).length > 0) out.workspaces = named(file.workspaces);
  if (Object.keys(named(file.repositories)).length > 0) out.repositories = named(file.repositories);
  if (file.defaultsOff.length > 0) out.defaultsOff = [...new Set(file.defaultsOff)].sort();

  writeTextAtomic(path, `${JSON.stringify(out, null, 2)}\n`);
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
  const circle = file.workspaces[normalizeWorkspace(workspace)];
  if (circle) layers.push(circle);
  const own = repository?.trim() ? file.repositories[repository.trim()] : undefined;
  if (own) layers.push(own);

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

/** `daoris agent rules …` — list, add to a list, remove, or switch a default. */
export function commandRules({ argv, write }: CommandArgs): ExitCode {
  const verb = argv[0] ?? 'list';
  const path = permissionsPath();
  const file = readPermissions(path);

  const workspace = flagValue(argv, '--workspace');
  const repository = flagValue(argv, '--repository');
  if (workspace !== undefined && repository !== undefined) {
    throw new DaorisError('a rule reaches one scope — name a circle with `--workspace` or a repository with `--repository`, not both.');
  }
  const scope: RuleScope = workspace !== undefined ? 'workspace' : repository !== undefined ? 'repository' : 'machine';
  const name = workspace ?? repository ?? null;
  const where = scope === 'machine' ? 'every session on this machine' : `${scope === 'workspace' ? 'circle' : 'repository'} \`${name}\``;

  switch (verb) {
    case 'list':
      return list();

    case 'allow':
    case 'ask':
    case 'deny': {
      const [rule] = operands('<rule>');
      writePermissions(path, addRule(file, scope, name, verb, rule!));
      write(`daoris: \`${rule}\` is now ${verb === 'allow' ? 'allowed' : verb === 'ask' ? 'asked for' : 'denied'} for ${where}.`);
      write('  Sessions started from now on are handed it; one already running keeps what it began with.');
      if (verb === 'ask') {
        write('  An ask is a refusal for anything Daoris starts — nobody is there to answer it.');
      }
      return 0;
    }

    case 'remove': {
      const [rule] = operands('<rule>');
      writePermissions(path, removeRule(file, scope, name, rule!));
      write(`daoris: \`${rule}\` is no longer one of the rules for ${where}.`);
      return 0;
    }

    case 'default': {
      const [id, state] = operands('<id>');
      if (state !== 'on' && state !== 'off') {
        throw new DaorisError(`say whether the default \`${id}\` is on or off: \`daoris agent rules default ${id} on|off\`.`);
      }
      writePermissions(path, switchDefault(file, id!, state === 'on'));
      write(`daoris: the default \`${id}\` is ${state} on this machine.`);
      return 0;
    }

    default:
      throw new DaorisError(
        `unknown rules verb '${verb}' — one of: list, allow, ask, deny, remove, default, proposals, accept, decline`);
  }

  /** The bare tokens after the verb — a flag and its value are not operands. At least one is needed. */
  function operands(what: string): string[] {
    const bare: string[] = [];
    for (let index = 1; index < argv.length; index += 1) {
      if (argv[index]!.startsWith('--')) {
        index += 1;
        continue;
      }
      bare.push(argv[index]!);
    }
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
    for (const [circle, held] of Object.entries(file.workspaces)) show(`circle \`${circle}\``, held);
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
    throw new DaorisError(`a ${scope === 'workspace' ? 'circle' : 'repository'} scope needs its name.`);
  }

  const key = name.trim();
  const field = scope === 'workspace' ? 'workspaces' : 'repositories';
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
