// `daoris agent rules proposals|accept|decline` — an agent's proposals to change what agents may do
// (PERM2, D74), answered from a terminal (D50).
//
// A session proposes over its connector (`permission_propose`), which writes one file per proposal
// under the home's `proposals/`. The driver settles each at its next tick: a narrowing is applied at
// once, a widening waits. 🔴 A widening never applies without the person — this is their door onto that
// yes, beside the screen's.
//
// It is the CLI's twin of the driver's `RuleProposals.cs`, and the service's `RuleProposalBox` writes
// the file. The three share no code, so THE FILE is the contract, and these rules hold in each:
//
//   1. A file that is not a proposal is left out of every list.
//   2. Only a proposal not yet settled is answered; a settled one is history.
//   3. Settling rewrites `state` and `settled` and keeps every other key.
//   4. A yes applies the change by the same edits `daoris agent rules` makes; a no changes nothing.
//
// It opens no socket and spawns nothing: it reads and writes files under the home.

import { existsSync, readdirSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { flagValue } from './args.ts';
import { DaorisError } from './errors.ts';
import type { ExitCode } from './errors.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { requireHomeFile } from './home.ts';
import {
  PERMISSIONS_FILE, addRule, readPermissions, removeRule, switchDefault, writePermissions,
} from './permissions.ts';
import type { PermissionFile, RuleList, RuleScope } from './permissions.ts';
import type { CommandArgs } from './types.ts';

export const PROPOSALS_FOLDER = 'proposals';

/** The verbs this module answers under `daoris agent rules`. */
export const PROPOSAL_VERBS: readonly string[] = ['proposals', 'accept', 'decline'];

export type ProposalState = 'proposed' | 'applied' | 'waiting' | 'accepted' | 'declined' | 'refused' | 'unchanged';

/** A change as proposed — the file's `change` object. */
export interface ProposedChange {
  action: string;
  scope: RuleScope;
  name: string | null;
  list: RuleList | null;
  rule: string | null;
  default: string | null;
  on: boolean | null;
}

export interface RuleProposal {
  id: string;
  proposed: string;
  session: string | null;
  ask: string | null;
  change: ProposedChange;
  why: string;
  state: ProposalState;
  settled: { at: string | null; by: string | null; note: string | null } | null;
}

const STATES: readonly ProposalState[] = ['proposed', 'applied', 'waiting', 'accepted', 'declined', 'refused', 'unchanged'];
const ID_SHAPE = /^[A-Za-z0-9_-]+$/;

/** Every readable proposal in the folder, newest first. A file that is not one is left out. */
export function loadProposals(folder: string): RuleProposal[] {
  if (!existsSync(folder)) return [];
  const read = readdirSync(folder)
    .filter((name) => name.endsWith('.json'))
    .map((name) => parse(join(folder, name)))
    .filter((proposal): proposal is RuleProposal => proposal !== null);
  const when = (proposal: RuleProposal) => Date.parse(proposal.proposed) || 0;
  return read.sort((a, b) => when(b) - when(a) || (a.id < b.id ? 1 : a.id > b.id ? -1 : 0));
}

/** The change in a line — the rule is the harness's own words, quoted. The driver says it the same way. */
export function describeChange(change: ProposedChange): string {
  const where = change.scope === 'machine'
    ? 'every session on this machine'
    : `${change.scope} \`${change.name}\``;
  switch (change.action) {
    case 'add': return `${change.list} \`${change.rule}\` for ${where}`;
    case 'remove': return `remove \`${change.rule}\` from ${where}`;
    default: return `switch the default \`${change.default}\` ${change.on === true ? 'on' : 'off'}`;
  }
}

/** Who proposed it, as a person reads it. */
export function authorOf(proposal: RuleProposal): string {
  if (!proposal.session) return 'a session the driver did not start';
  return proposal.ask ? `session ${proposal.session} (ask #${proposal.ask})` : `session ${proposal.session}`;
}

/**
 * The person's answer: a yes applies the change whatever it does, a no changes nothing. Only a proposal
 * not yet settled is answered, and a yes the rules cannot take writes nothing at all.
 */
export function answerProposal(home: string, id: string, accept: boolean, note: string | null, at: string): RuleProposal {
  const key = id.trim().replace(/^#/, '');
  const path = join(home, PROPOSALS_FOLDER, `${key}.json`);
  // An id names a file in the folder, and a path is not an id.
  const proposal = ID_SHAPE.test(key) && existsSync(path) ? parse(path) : null;
  if (!proposal) {
    throw new DaorisError(`no proposal \`#${key}\` on this machine — \`daoris agent rules proposals\` lists them.`);
  }
  if (proposal.state !== 'proposed' && proposal.state !== 'waiting') {
    throw new DaorisError(`proposal \`#${key}\` was already ${proposal.state} — it is history now.`);
  }

  if (accept) {
    const rulesPath = join(home, PERMISSIONS_FILE);
    writePermissions(rulesPath, apply(readPermissions(rulesPath), proposal.change));
  }

  const state: ProposalState = accept ? 'accepted' : 'declined';
  const kept = note?.trim() ? note.trim() : null;
  mark(path, state, 'the person', kept, at);
  return { ...proposal, state, settled: { at, by: 'the person', note: kept } };
}

/** `daoris agent rules proposals | accept <id> | decline <id> [--note "…"]`. */
export function commandProposals({ argv, write }: CommandArgs): ExitCode {
  const verb = argv[0] ?? 'proposals';
  const folder = requireHomeFile(process.env, PROPOSALS_FOLDER);
  const home = dirname(folder);

  if (verb === 'proposals') return list();

  const note = flagValue(argv, '--note') ?? null;
  const id = argv.slice(1).find((token, index, rest) => !token.startsWith('--') && rest[index - 1] !== '--note');
  if (!id) throw new DaorisError(`\`daoris agent rules ${verb}\` needs the proposal's id — \`daoris agent rules proposals\` lists them.`);

  const answered = answerProposal(home, id, verb === 'accept', note, new Date().toISOString());
  write(verb === 'accept'
    ? `daoris: accepted #${answered.id} — ${describeChange(answered.change)} is now in the rules.`
    : `daoris: declined #${answered.id} — the rules are as they were.`);
  if (verb === 'accept') write('  Sessions started from now on are handed it; one already running keeps what it began with.');
  return 0;

  function list(): ExitCode {
    const proposals = loadProposals(folder);
    write(`Agents' proposals to change these rules — ${folder}`);
    write('  A narrowing applies at the driver\'s next tick; a widening waits for your yes.');
    if (proposals.length === 0) {
      write('');
      write('No agent has proposed a change.');
      return 0;
    }

    for (const proposal of proposals) {
      write('');
      write(`#${proposal.id}  ${proposal.state.padEnd(9)} ${describeChange(proposal.change)}`);
      write(`  from ${authorOf(proposal)}, ${proposal.proposed}`);
      write(`  why: ${proposal.why}`);
      if (proposal.settled?.by) {
        write(`  ${proposal.state} by ${proposal.settled.by}${proposal.settled.note ? `: ${proposal.settled.note}` : ''}`);
      }
    }

    const open = proposals.filter((p) => p.state === 'proposed' || p.state === 'waiting').length;
    if (open > 0) {
      write('');
      write(`${open} ${open === 1 ? 'waits' : 'wait'} for an answer: \`daoris agent rules accept <id>\` or `
        + '`daoris agent rules decline <id> [--note "…"]`.');
    }
    return 0;
  }
}

/** The rules with the change made — by the same edits `daoris agent rules` makes. */
function apply(file: PermissionFile, change: ProposedChange): PermissionFile {
  switch (change.action) {
    case 'add':
      if (!change.list) throw new DaorisError('a rule goes in `allow`, `ask` or `deny`.');
      return addRule(file, change.scope, change.name, change.list, change.rule ?? '');
    case 'remove':
      return removeRule(file, change.scope, change.name, change.rule ?? '');
    case 'default':
      if (change.on === null) throw new DaorisError(`say whether the default \`${change.default}\` goes on or off.`);
      return switchDefault(file, change.default ?? '', change.on);
    default:
      throw new DaorisError(`a proposal is to \`add\`, \`remove\` or \`default\`, not \`${change.action}\`.`);
  }
}

function parse(path: string): RuleProposal | null {
  try {
    const root = readJsonObject(path).value;
    if (root === null) return null;
    const id = text(root.id);
    const change = record(root?.change);
    if (!id || !change) return null;

    const by = record(root.by);
    const settled = record(root.settled);
    const scope = text(change.scope);
    const list = text(change.list);
    const state = text(root.state) as ProposalState | null;
    return {
      id,
      proposed: text(root.proposed) ?? statSync(path).mtime.toISOString(),
      session: text(by?.session),
      ask: text(by?.ask),
      change: {
        action: text(change.action) ?? '',
        scope: scope === 'workspace' || scope === 'repository' ? scope : 'machine',
        name: text(change.name),
        list: list === 'allow' || list === 'ask' || list === 'deny' ? list : null,
        rule: text(change.rule),
        default: text(change.default),
        on: typeof change.on === 'boolean' ? change.on : null,
      },
      why: text(root.why) ?? '',
      state: state && STATES.includes(state) ? state : 'proposed',
      settled: settled ? { at: text(settled.at), by: text(settled.by), note: text(settled.note) } : null,
    };
  } catch {
    return null;
  }
}

/** Rewrite the file's state and settling, keeping every other key the session wrote. */
function mark(path: string, state: ProposalState, by: string, note: string | null, at: string): void {
  const root = readJsonObject(path).value ?? {};
  root.state = state;
  root.settled = { at, by, note };
  writeJsonAtomic(path, root);
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

function record(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null;
}
