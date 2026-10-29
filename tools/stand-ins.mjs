/**
 * The stand-ins, found from their files (AGENTPROVIDER1 row b, part 3; the agent-provider design §11 row b; D213's row b part 3 note).
 *
 * A provider written on the contract keeps a `provider.json` in its folder under the driver's `Agents/Providers/`: its id, its stand-in
 * flag, its doors' ids and kinds and, for a stand-in, its program's layout, the entry under the folder's `Program/` (a Node module) and
 * the arguments that choose each door. It is the one place a tool reads those facts, and the driver's `ProviderFilesTests` holds each
 * file equal to `Providers.Built()` field by field. Beside it, the provider's `pending.json` holds the cases it cannot pass yet, each with
 * the row that makes it pass, read here as the conformance suite reads it.
 *
 * The scenario contract (`SCENARIO_CONTRACT`) is the cases a stand-in's program answers in its own vocabulary, which the family
 * rehearsal's stand-in phase drives through the driver over every stand-in the files declare (`scenarioPlan`). Each case is named as
 * the conformance suite's live case of the same clause, so one line of a provider's `pending.json` holds it in both. A later stand-in is
 * its folder alone: nothing here, in the rehearsal or in the suite names one.
 *
 *   node tools/stand-ins.mjs     list every stand-in the files declare, its doors and each case of the contract on each (exit 0, 1 a
 *                                file out of the layout, 2 tool error)
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

/** The driver's providers' folder, under the repository's root. */
export const providersFolder = (repoRoot) => join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Agents', 'Providers');

/** The folder a provider's program sits in, which the driver project's one glob carries beside the driver at the same path. */
export const PROGRAM_FOLDER = 'Program';

export const PROVIDER_FILE = 'provider.json';
export const PENDING_FILE = 'pending.json';

/** A door's kinds: the native door's own process per run, or the protocol door's conversation over stdio. */
export const KINDS = Object.freeze(['native', 'protocol']);

/** A pending line's door for every door the case runs on. */
export const EVERY_DOOR = '*';

const FIELDS = ['id', 'standIn', 'program', 'doors'];
const DOOR_FIELDS = ['id', 'kind', 'arguments'];
const LINE_FIELDS = ['case', 'door', 'row', 'why'];

const words = (value) => Array.isArray(value) && value.every((each) => typeof each === 'string' && each.trim().length > 0);
const text = (value) => (typeof value === 'string' && value.trim().length > 0 ? value : null);

/**
 * One provider's file read by the one layout: the provider, or null where the file breaks it, and each break named. `folder` is the
 * provider folder's name (`Stub`), which is its path under the providers' folder in the source and in a build's output alike.
 */
export function readProvider(file, folder) {
  const where = `${folder}/${PROVIDER_FILE}`;
  let json;
  try {
    json = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    return { provider: null, problems: [`${where}: not JSON (${error.message})`] };
  }
  if (json === null || typeof json !== 'object' || Array.isArray(json)) {
    return { provider: null, problems: [`${where}: the file is an object of ${FIELDS.join(', ')}`] };
  }

  const problems = Object.keys(json).filter((name) => !FIELDS.includes(name))
    .map((name) => `${where}: '${name}' is no field of the layout (${FIELDS.join(', ')})`);
  const id = text(json.id);
  if (id === null) problems.push(`${where}: 'id' is missing or blank`);
  if (typeof json.standIn !== 'boolean') problems.push(`${where}: 'standIn' is true or false`);
  const standIn = json.standIn === true;

  const entry = text(json.program?.entry);
  if (json.program !== undefined && entry === null) problems.push(`${where}: 'program.entry' is missing or blank`);
  if (entry !== null && (entry.includes('..') || /^([a-z]:)?[\\/]/i.test(entry))) {
    problems.push(`${where}: 'program.entry' is a path under the folder's ${PROGRAM_FOLDER}/, never outside it: ${entry}`);
  }
  if (standIn && json.program === undefined) problems.push(`${where}: a stand-in states its 'program', the entry under its folder's ${PROGRAM_FOLDER}/`);

  const doors = [];
  if (!Array.isArray(json.doors) || json.doors.length === 0) {
    problems.push(`${where}: 'doors' is an array of ${DOOR_FIELDS.join(', ')}, one at least`);
  } else {
    json.doors.forEach((door, at) => {
      const on = `${where}, door ${at + 1}`;
      if (door === null || typeof door !== 'object' || Array.isArray(door)) {
        problems.push(`${on}: a door is an object of ${DOOR_FIELDS.join(', ')}`);
        return;
      }
      problems.push(...Object.keys(door).filter((name) => !DOOR_FIELDS.includes(name))
        .map((name) => `${on}: '${name}' is no field of the layout (${DOOR_FIELDS.join(', ')})`));
      if (text(door.id) === null) problems.push(`${on}: 'id' is missing or blank`);
      if (!KINDS.includes(door.kind)) problems.push(`${on}: 'kind' is one of ${KINDS.join(', ')}`);
      if (door.arguments !== undefined && !words(door.arguments)) problems.push(`${on}: 'arguments' is an array of words`);
      if (standIn && !(words(door.arguments) && door.arguments.length > 0)) {
        problems.push(`${on}: a stand-in's door states the 'arguments' that choose it in its program`);
      }
      if (text(door.id) !== null && KINDS.includes(door.kind)) {
        doors.push({ id: door.id, kind: door.kind, arguments: words(door.arguments) ? [...door.arguments] : [] });
      }
    });
  }

  return problems.length > 0
    ? { provider: null, problems }
    : { provider: { id, standIn, folder, program: entry === null ? null : { entry }, doors }, problems: [] };
}

/** A provider's pending cases, read from beside its file; a stand-in brings one, `[]` where it passes every case. */
export function readPending(file, folder) {
  const where = `${folder}/${PENDING_FILE}`;
  if (!existsSync(file)) return { lines: [], problems: [`${where}: missing; each provider brings its own, [] where it passes every case`] };
  let json;
  try {
    json = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    return { lines: [], problems: [`${where}: not JSON (${error.message})`] };
  }
  if (!Array.isArray(json)) return { lines: [], problems: [`${where}: the file is an array of lines`] };
  const lines = [];
  const problems = [];
  json.forEach((line, at) => {
    if (line === null || typeof line !== 'object' || !LINE_FIELDS.every((name) => text(line[name]) !== null)) {
      problems.push(`${where}, line ${at + 1}: a line is an object of ${LINE_FIELDS.join(', ')}, none blank`);
      return;
    }
    lines.push({ case: line.case, door: line.door, row: line.row, why: line.why });
  });
  return { lines, problems };
}

/**
 * Every provider the files declare under `folder` (the providers' folder), in the folders' order, each with its pending lines, and
 * every break of the layout named. A folder with no `provider.json` is no provider's.
 */
export function readProviders(folder) {
  const providers = [];
  const problems = [];
  const names = existsSync(folder)
    ? readdirSync(folder, { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name).sort()
    : [];
  for (const name of names) {
    const file = join(folder, name, PROVIDER_FILE);
    if (!existsSync(file)) continue;
    const read = readProvider(file, name);
    problems.push(...read.problems);
    if (read.provider === null) continue;
    const pending = readPending(join(folder, name, PENDING_FILE), name);
    if (read.provider.standIn) problems.push(...pending.problems);
    providers.push({ ...read.provider, pending: pending.lines });
  }
  for (const twice of providers.map((each) => each.id.toLowerCase()).filter((id, at, all) => all.indexOf(id) !== at)) {
    problems.push(`provider '${twice}' is declared by two folders' ${PROVIDER_FILE}`);
  }
  return { providers, problems };
}

/** The stand-ins alone: every provider whose file says it runs no model. */
export function readStandIns(folder) {
  const { providers, problems } = readProviders(folder);
  return { standIns: providers.filter((provider) => provider.standIn), problems };
}

/** A stand-in's program under `root`: the driver's source folder, or a build's output that carries it beside the driver. */
export const programOf = (standIn, root) => join(root, 'Agents', 'Providers', standIn.folder, PROGRAM_FOLDER, standIn.program.entry);

/** The command that runs a door's program from `root`: Node, the entry, the door's own arguments, then the case's. */
export const doorCommand = (standIn, door, root, more = []) => ['node', programOf(standIn, root), ...door.arguments, ...more];

/** The line of a provider's pending cases holding a case on a door: one naming that door, or every door. */
export const heldBy = (lines, name, door) =>
  lines.find((line) => line.case === name && (line.door === EVERY_DOOR || line.door === door)) ?? null;

/**
 * The scenario contract: each case a stand-in's program answers, by the conformance suite's name for its clause, the door kinds it
 * applies to, and what it says. The order is the order the rehearsal drives them in on each provider, every case on every door before
 * the next: the cases that leave an account refused or cooling come last. `elsewhere` names the one reader of a case no verb of the
 * driver can drive for a stand-in, said rather than checked.
 */
export const SCENARIO_CONTRACT = Object.freeze([
  { case: 'live.start', kinds: KINDS, says: 'a start: a quest run on the door to done, taken and closed by the program' },
  { case: 'live.turn', kinds: KINDS, says: 'a turn: a conversation from a terminal, each line the person says said back' },
  {
    case: 'live.resume-own', kinds: KINDS,
    says: 'a resume of its own conversation: words said to its ended session go on in it, or, where its door resumes nothing, the unsupported resume\'s fallback',
  },
  { case: 'live.missing-conversation', kinds: ['protocol'], says: 'a missing one: a resume the agent refuses as gone, read as gone' },
  { case: 'live.child-seen-ended', kinds: KINDS, says: 'a child seen and ended, read through the provider\'s child vocabulary' },
  {
    case: 'live.sign-in', kinds: KINDS, says: 'a sign-in through the roster\'s sign-in and login question',
    elsewhere: 'the roster\'s sign-in is the person\'s press on the desktop and no verb takes a stand-in as its operand, so the conformance suite\'s Process half reads it (live.sign-in)',
  },
  { case: 'live.access-and-fatal', kinds: KINDS, says: 'a refused account: the credential refused at its run, which ends failed with its quest not done' },
  { case: 'live.limit-cools', kinds: KINDS, says: 'a limit cooling the bound account with no strike' },
]);

/**
 * The phase's plan: for each stand-in, each case of the contract, each door it applies to, whether the provider's pending file holds it
 * (the line, said with its row and never driven), it is read elsewhere (`elsewhere`), or it is driven.
 */
export function scenarioPlan(standIns) {
  const rows = [];
  for (const standIn of standIns) {
    for (const scenario of SCENARIO_CONTRACT) {
      for (const door of standIn.doors.filter((each) => scenario.kinds.includes(each.kind))) {
        const held = heldBy(standIn.pending, scenario.case, door.id);
        rows.push({
          provider: standIn.id, door: door.id, kind: door.kind, case: scenario.case, says: scenario.says,
          held, elsewhere: held ? null : scenario.elsewhere ?? null,
        });
      }
    }
  }
  return rows;
}

if (isMain(import.meta.url)) {
  try {
    const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
    const { standIns, problems } = readStandIns(providersFolder(repoRoot));
    for (const standIn of standIns) {
      console.log(`${standIn.id}: ${standIn.folder}/${PROGRAM_FOLDER}/${standIn.program.entry}`);
      for (const door of standIn.doors) console.log(`  ${door.id} (${door.kind}): ${door.arguments.join(' ')}`);
    }
    for (const row of scenarioPlan(standIns)) {
      const state = row.held ? `held (row ${row.held.row})` : row.elsewhere ? 'read elsewhere' : 'driven';
      console.log(`  ${row.case} on ${row.door}: ${state}`);
    }
    for (const problem of problems) console.log(`problem: ${problem}`);
    process.exitCode = problems.length > 0 ? 1 : 0;
  } catch (error) {
    console.error(`stand-ins: ${error.message}`);
    process.exitCode = 2;
  }
}
